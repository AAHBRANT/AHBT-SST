import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { api, StatusProcessoEleitoralCipa, type CandidatoUrnaCipa, type CedulaCipa, type Trabalhador, type UrnaCipa } from '../../../lib/api';
import {
  capturarDigitalLocal,
  credencialLeitura,
  estaAgenteLocalDisponivel,
  obterDispositivoLocal,
  type DispositivoLocal,
} from '../../../lib/agenteBiometricoLocal';
import { FilaFacialDds, type ResultadoFilaFacial } from '../../../components/dds/FilaFacialDds';
import { tocarCliqueUrna, tocarSomFimUrna } from './somUrna';
import './urnaCipa.css';

// Urna biométrica da CIPA no tablet do canteiro (16/11/2026, protótipo aprovado pelo usuário).
// Fluxo com o mínimo de toques: digital (0 toques, o leitor fica escutando) ou rosto (1 toque) → toque
// na foto → CONFIRMA → som + "FIM" → volta sozinha. Só aparecem candidatos DEFERIDOS (decisão do
// usuário, 16/11). Nenhuma parcial de votos é mostrada: só "X de Y votaram".
// Sigilo: a tela guarda a cédula só em memória e a descarta ao votar/expirar; o voto vai ao servidor
// com a cédula, nunca com o Id do trabalhador (ver UrnaCipaCommands.cs).

type Tela = 'espera' | 'grade' | 'fim' | 'javotou' | 'erro' | 'mesario' | 'fechada';

const TEMPO_PARA_VOTAR_MS = 60_000;
const VOLTA_FIM_MS = 3_500;
const VOLTA_AVISO_MS = 4_000;

function iniciais(nome: string): string {
  const p = nome.trim().split(/\s+/);
  return ((p[0]?.[0] ?? '') + (p.length > 1 ? p[p.length - 1][0] : '')).toUpperCase();
}

function primeiroNome(nome: string) {
  return nome.trim().split(/\s+/)[0] ?? nome;
}

const CORES = ['#8a4b2b', '#5b3a6b', '#2f5b6b', '#6b5a2f', '#2f6b45', '#6b2f3a', '#3a4f7a', '#7a5a3a'];

function Foto({ candidato, url, indice, className }: { candidato: CandidatoUrnaCipa; url?: string; indice: number; className?: string }) {
  return url ? (
    <img className={`urna-foto ${className ?? ''}`} src={url} alt="" />
  ) : (
    <span className={`urna-foto ${className ?? ''}`} style={{ background: CORES[indice % CORES.length] }} aria-hidden>
      {iniciais(candidato.nome)}
    </span>
  );
}

export function UrnaCipaPage() {
  const { id = '' } = useParams<{ id: string }>();
  const navegar = useNavigate();
  const [urna, setUrna] = useState<UrnaCipa | null>(null);
  const [fotos, setFotos] = useState<Record<string, string>>({});
  const [tela, setTela] = useState<Tela>('espera');
  const [cedula, setCedula] = useState<CedulaCipa | null>(null);
  const [escolhido, setEscolhido] = useState<CandidatoUrnaCipa | null>(null);
  const [mensagem, setMensagem] = useState('');
  const [enviando, setEnviando] = useState(false);
  const [leitor, setLeitor] = useState<DispositivoLocal | null>(null);
  const [lendo, setLendo] = useState(false);
  const [facialAberta, setFacialAberta] = useState(false);
  const [eleitores, setEleitores] = useState<Trabalhador[]>([]);
  const [busca, setBusca] = useState('');
  const telaRef = useRef(tela);
  useEffect(() => {
    telaRef.current = tela;
  }, [tela]);
  const timer = useRef<number | undefined>(undefined);

  const carregar = useCallback(async () => {
    const dados = await api.cipa.urna.obter(id);
    setUrna(dados);
    if (dados.status !== StatusProcessoEleitoralCipa.VotacaoAberta) setTela('fechada');
    return dados;
  }, [id]);

  // Carrega a urna e as fotos dos candidatos uma vez.
  useEffect(() => {
    let urls: string[] = [];
    void carregar()
      .then(async (dados) => {
        const pares = await Promise.all(
          dados.candidatos.filter((c) => c.temFoto).map(async (c) => {
            const blob = await api.cipa.urna.foto(id, c.candidatoId).catch(() => null);
            return blob ? ([c.candidatoId, URL.createObjectURL(blob)] as const) : null;
          }),
        );
        const mapa: Record<string, string> = {};
        pares.forEach((p) => p && (mapa[p[0]] = p[1]));
        urls = Object.values(mapa);
        setFotos(mapa);
      })
      .catch((e) => {
        setMensagem(e instanceof Error ? e.message : 'Não foi possível abrir a urna.');
        setTela('erro');
      });
    return () => urls.forEach((u) => URL.revokeObjectURL(u));
  }, [carregar, id]);

  // Leitor de digital, se houver agente local nesta máquina.
  useEffect(() => {
    void estaAgenteLocalDisponivel().then((ok) => (ok ? obterDispositivoLocal().then(setLeitor).catch(() => setLeitor(null)) : setLeitor(null)));
  }, []);

  const voltarAoInicio = useCallback(() => {
    window.clearTimeout(timer.current);
    setCedula(null);
    setEscolhido(null);
    setEnviando(false);
    setTela((t) => (t === 'fechada' ? t : 'espera'));
  }, []);

  const agendar = useCallback((ms: number, depois: () => void) => {
    window.clearTimeout(timer.current);
    timer.current = window.setTimeout(depois, ms);
  }, []);

  useEffect(() => () => window.clearTimeout(timer.current), []);

  const receberCedula = useCallback(
    (c: CedulaCipa) => {
      setCedula(c);
      setEscolhido(null);
      setTela('grade');
      agendar(TEMPO_PARA_VOTAR_MS, voltarAoInicio);
    },
    [agendar, voltarAoInicio],
  );

  const tratarErroIdentificacao = useCallback(
    (e: unknown) => {
      const texto = e instanceof Error ? e.message : 'Não foi possível identificar.';
      setMensagem(texto);
      setTela(/já votou/i.test(texto) ? 'javotou' : 'erro');
      agendar(VOLTA_AVISO_MS, voltarAoInicio);
    },
    [agendar, voltarAoInicio],
  );

  // Laço do leitor de digital: fica escutando enquanto a tela está em "espera" (0 toques).
  useEffect(() => {
    if (!leitor || tela !== 'espera') return;
    let ativo = true;
    void (async () => {
      while (ativo && telaRef.current === 'espera') {
        try {
          const captura = await capturarDigitalLocal(leitor.dispositivoId);
          if (!ativo || telaRef.current !== 'espera') break;
          setLendo(true);
          tocarCliqueUrna();
          const c = await api.cipa.urna.identificarDigital(id, captura.trabalhadorId, credencialLeitura(leitor, captura.leituras[0]), captura.score);
          receberCedula(c);
          break;
        } catch (e) {
          const texto = e instanceof Error ? e.message : '';
          if (/Nenhum dedo detectado|dentro do tempo limite/i.test(texto)) continue;
          if (e instanceof TypeError) {
            setLeitor(null);
            break;
          }
          tratarErroIdentificacao(e);
          break;
        } finally {
          setLendo(false);
        }
      }
    })();
    return () => {
      ativo = false;
    };
  }, [leitor, tela, id, receberCedula, tratarErroIdentificacao]);

  async function enviarFotoFacial(foto: File): Promise<ResultadoFilaFacial> {
    try {
      const c = await api.cipa.urna.identificarFacial(id, foto);
      setFacialAberta(false);
      receberCedula(c);
      return { trabalhadorNome: c.eleitorNome, jaConfirmado: false };
    } catch (e) {
      if (e instanceof Error && /já votou/i.test(e.message)) {
        setFacialAberta(false);
        tratarErroIdentificacao(e);
        return { trabalhadorNome: 'Eleitor', jaConfirmado: true };
      }
      throw e;
    }
  }

  function escolher(c: CandidatoUrnaCipa) {
    tocarCliqueUrna();
    setEscolhido(c);
    agendar(TEMPO_PARA_VOTAR_MS, voltarAoInicio);
  }

  function corrigir() {
    tocarCliqueUrna();
    setEscolhido(null);
  }

  async function confirmar() {
    if (!cedula || !escolhido || enviando) return;
    setEnviando(true);
    try {
      const r = await api.cipa.urna.votar(id, cedula.cedulaId, escolhido.candidatoId);
      tocarSomFimUrna();
      setUrna((u) => (u ? { ...u, totalVotantes: r.totalVotantes } : u));
      setCedula(null);
      setEscolhido(null);
      setTela('fim');
      agendar(VOLTA_FIM_MS, voltarAoInicio);
    } catch (e) {
      tratarErroIdentificacao(e);
    } finally {
      setEnviando(false);
    }
  }

  async function abrirMesario() {
    window.clearTimeout(timer.current);
    setBusca('');
    setTela('mesario');
    if (urna && eleitores.length === 0) {
      const lista = await api.trabalhadores.listar(urna.obraId).catch(() => []);
      setEleitores(lista.filter((t) => !t.dataDemissao && t.vinculo === 1).sort((a, b) => a.nome.localeCompare(b.nome)));
    }
  }

  async function liberar(t: Trabalhador) {
    try {
      receberCedula(await api.cipa.urna.liberarMesario(id, t.id));
    } catch (e) {
      tratarErroIdentificacao(e);
    }
  }

  const candidatos = urna?.candidatos ?? [];
  const n = candidatos.length;
  const colunas = n <= 4 ? Math.max(n, 1) : n <= 6 ? 3 : n <= 8 ? 4 : n <= 12 ? 4 : 6;
  const filtrados = eleitores.filter((t) => t.nome.toLowerCase().includes(busca.trim().toLowerCase())).slice(0, 30);

  return (
    <div className="urna-raiz" role="application" aria-label="Urna da CIPA">
      <header className="urna-barra">
        <div className="urna-barra-t">
          <span className="urna-logo">AHBT</span>
          <div>
            <b>Eleição CIPA{urna?.numeroDocumento ? ` · ${urna.numeroDocumento}` : ''}</b>
            <small>{urna?.obraNome ?? 'Carregando…'}</small>
          </div>
        </div>
        <div className="urna-barra-d">
          {urna && (
            <span className="urna-cnt">
              {urna.totalVotantes} de {urna.totalEleitoresAptos} votaram
            </span>
          )}
          <button type="button" className="urna-sair" onClick={() => navegar(`/operacao/cipa/eleicao/${id}`)}>
            Sair
          </button>
        </div>
      </header>

      {tela === 'espera' && (
        <section className="urna-view urna-espera">
          <div className={`urna-sensor ${lendo ? 'lendo' : ''} ${leitor ? '' : 'sem-leitor'}`} aria-hidden>
            <svg viewBox="0 0 24 24" fill="none" strokeWidth="1.5" strokeLinecap="round">
              <path d="M12 11v3a8 8 0 0 1-1 4" /><path d="M8.5 7.6A5 5 0 0 1 17 11v1.5" /><path d="M7 11a5 5 0 0 1 .3-1.7" />
              <path d="M7 14.5a12 12 0 0 1-.6 3" /><path d="M14.5 14a14 14 0 0 1-.9 5" /><path d="M5 5.6A9 9 0 0 1 21 11" />
              <path d="M3 9a9 9 0 0 1 .6-1.6" /><path d="M19.5 15a20 20 0 0 1-.5 3" />
            </svg>
            <span className="urna-scan" />
          </div>
          <h2>{leitor ? 'Coloque o dedo no leitor' : 'Toque abaixo para usar o rosto'}</h2>
          {!leitor && <p>Leitor de digital não encontrado neste tablet.</p>}
          <div className="urna-alt">
            <button type="button" className="urna-btn-rosto" onClick={() => setFacialAberta(true)}>
              {leitor ? 'ou use o rosto' : 'Usar o rosto'}
            </button>
            <button type="button" className="urna-btn-link" onClick={() => void abrirMesario()}>
              Mesário
            </button>
          </div>
        </section>
      )}

      {tela === 'grade' && cedula && (
        <section className="urna-view">
          <div className="urna-ola">
            <div>
              <h2>Olá, {primeiroNome(cedula.eleitorNome)}! Toque na foto do seu candidato.</h2>
              <small>
                {cedula.eleitorNome} · {cedula.eleitorFuncao}
              </small>
            </div>
            <button type="button" className="urna-btn-link" onClick={voltarAoInicio}>
              Cancelar
            </button>
          </div>
          <div className={`urna-grade ${escolhido ? 'sel' : ''}`} style={{ ['--cols' as string]: colunas }}>
            {candidatos.map((c, i) => (
              <button
                key={c.candidatoId}
                type="button"
                className={`urna-cand ${escolhido?.candidatoId === c.candidatoId ? 'on' : ''}`}
                onClick={() => escolher(c)}
                aria-label={`${c.nome}, ${c.funcao}`}
              >
                <Foto candidato={c} url={fotos[c.candidatoId]} indice={i} />
                <b>{primeiroNome(c.nome)}</b>
                <small>{c.funcao}</small>
              </button>
            ))}
          </div>

          {escolhido && (
            <div className="urna-sheet" onClick={(e) => e.target === e.currentTarget && corrigir()}>
              <div className="urna-card" role="dialog" aria-modal="true" aria-labelledby="urna-nome">
                <div className="urna-louro">
                  <Louro />
                  <Foto candidato={escolhido} url={fotos[escolhido.candidatoId]} indice={candidatos.indexOf(escolhido)} className="grande" />
                </div>
                <h3 id="urna-nome">{escolhido.nome}</h3>
                <span className="urna-fn">{escolhido.funcao}</span>
                <span className="urna-q">Confirma seu voto?</span>
                <div className="urna-acts">
                  <button type="button" className="urna-corr" onClick={corrigir} disabled={enviando}>
                    CORRIGE
                  </button>
                  <button type="button" className="urna-conf" onClick={() => void confirmar()} disabled={enviando} autoFocus>
                    {enviando ? '...' : 'CONFIRMA'}
                  </button>
                </div>
              </div>
            </div>
          )}
        </section>
      )}

      {tela === 'fim' && (
        <section className="urna-view urna-fim">
          <span className="urna-check">✓</span>
          <span className="urna-big">FIM</span>
          <p>Voto registrado. Obrigado!</p>
          <small>Seu voto é secreto: ninguém vê em quem você votou.</small>
        </section>
      )}

      {(tela === 'javotou' || tela === 'erro') && (
        <section className="urna-view urna-msg">
          <span className="urna-ic" style={{ background: tela === 'javotou' ? '#670000' : '#e0a400' }}>
            {tela === 'javotou' ? '✓' : '?'}
          </span>
          <h2>{tela === 'javotou' ? 'Você já votou' : 'Não foi possível identificar'}</h2>
          <p>{tela === 'javotou' ? 'Cada funcionário vota uma única vez.' : mensagem}</p>
          <div className="urna-alt">
            <button type="button" className="urna-btn-rosto" onClick={voltarAoInicio}>
              Voltar
            </button>
          </div>
        </section>
      )}

      {tela === 'mesario' && (
        <section className="urna-view urna-mesario">
          <h2>Liberação pelo mesário</h2>
          <p>Use só quando a biometria não funcionar. Fica registrado na ata quem liberou.</p>
          <input
            className="urna-busca"
            placeholder="Nome do eleitor"
            value={busca}
            onChange={(e) => setBusca(e.target.value)}
            autoFocus
            aria-label="Buscar eleitor"
          />
          <div className="urna-lista">
            {filtrados.map((t) => (
              <button key={t.id} type="button" onClick={() => void liberar(t)}>
                {t.nome}
              </button>
            ))}
          </div>
          <button type="button" className="urna-btn-link" onClick={voltarAoInicio}>
            Voltar
          </button>
        </section>
      )}

      {tela === 'fechada' && (
        <section className="urna-view urna-msg">
          <span className="urna-ic" style={{ background: '#4b4b4b' }}>🔒</span>
          <h2>Votação fechada</h2>
          <p>
            {urna?.status === StatusProcessoEleitoralCipa.Apurado
              ? 'A votação foi encerrada e apurada. O resultado está no painel da CIPA.'
              : 'A votação desta eleição não está aberta. O técnico abre a votação na tela do processo eleitoral.'}
          </p>
          <div className="urna-alt">
            <button type="button" className="urna-btn-rosto" onClick={() => void carregar().then((d) => d.status === StatusProcessoEleitoralCipa.VotacaoAberta && setTela('espera'))}>
              Atualizar
            </button>
          </div>
        </section>
      )}

      <FilaFacialDds
        aberto={facialAberta}
        aoFechar={() => setFacialAberta(false)}
        aoPresencaConfirmada={() => undefined}
        enviarFoto={enviarFotoFacial}
        titulo="Urna da CIPA"
        subtitulo="Olhe para a câmera"
      />
    </div>
  );
}

// Louros dourados em volta da foto no cartão de confirmação (mesmo desenho do mockup aprovado).
function Louro() {
  const r = 86;
  const ponto = (t: number) => [100 + r * Math.cos((t * Math.PI) / 180), 100 + r * Math.sin((t * Math.PI) / 180)];
  const ramos: [number, number][] = [
    [100, 218],
    [80, -38],
  ];
  return (
    <svg className="urna-louro-svg" viewBox="0 0 200 200" aria-hidden>
      <defs>
        <linearGradient id="urna-ouro" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="#fff2c2" />
          <stop offset=".5" stopColor="#e3b85a" />
          <stop offset="1" stopColor="#9c7124" />
        </linearGradient>
      </defs>
      {ramos.map(([t0, t1]) => {
        const [x0, y0] = ponto(t0);
        const [x1, y1] = ponto(t1);
        const dir = t1 > t0 ? 1 : -1;
        return (
          <g key={t0}>
            <path d={`M${x0} ${y0} A${r} ${r} 0 0 ${dir > 0 ? 1 : 0} ${x1} ${y1}`} stroke="url(#urna-ouro)" strokeWidth="2.5" fill="none" />
            {Array.from({ length: 8 }, (_, i) => {
              const t = t0 + ((t1 - t0) * (i + 0.6)) / 8;
              const sz = 1 - i * 0.04;
              return ([[-1, r + 9], [1, r - 9]] as const).map(([s, rr]) => {
                const x = 100 + rr * Math.cos((t * Math.PI) / 180);
                const y = 100 + rr * Math.sin((t * Math.PI) / 180);
                return (
                  <ellipse key={`${i}${s}`} cx={x} cy={y} rx={5.5 * sz} ry={13 * sz} fill="url(#urna-ouro)"
                    transform={`rotate(${t + (dir > 0 ? 0 : 180) + s * 28 * dir} ${x} ${y})`} />
                );
              });
            })}
          </g>
        );
      })}
    </svg>
  );
}

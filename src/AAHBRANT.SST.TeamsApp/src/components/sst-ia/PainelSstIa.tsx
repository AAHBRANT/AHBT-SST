import { useCallback, useEffect, useRef, useState, type KeyboardEvent } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
// makeStyles ainda não tem reexport em @ui (mesma exceção pontual da SuporteIaTab).
// oxlint-disable-next-line no-restricted-imports
import { makeStyles, mergeClasses } from '@fluentui/react-components';
import {
  Dismiss20Regular,
  Mic20Regular,
  RecordStop20Filled,
  Send20Filled,
  Speaker220Regular,
  SpeakerMute20Regular,
  Stop16Filled,
} from '@fluentui/react-icons';
import {
  api,
  SeveridadeSolicitacaoSuporteIa,
  severidadeSolicitacaoSuporteIaLabel,
  statusSolicitacaoSuporteIaLabel,
  TipoSolicitacaoSuporteIa,
  tipoSolicitacaoSuporteIaLabel,
  type SuporteIaSolicitacao,
} from '../../lib/api';
import { Button, FeedbackInline, Input, Select, Spinner, Textarea, useEstilosEsmeralda } from '@ui';
import { IndicadorGravacao } from '../voz/IndicadorGravacao';
import { useGravacaoVoz } from '../voz/useGravacaoVoz';
import { IconeSstIa } from './IconeSstIa';
import { TextoSstIa } from './TextoSstIa';
import { STATUS_COM_NOVIDADE } from './statusSstIa';
import type { MensagemSstIa } from './useConversaSstIa';
import type { useConversaSstIa } from './useConversaSstIa';
import type { useLeituraVoz } from './useLeituraVoz';

// Painel do SST IA (mockups aprovados em 18/11/2026: docs/mockups/sst-ia-*.html). Topo no vidro
// esmeralda do Início; três abas: Perguntar (agente), Abrir chamado e Meus chamados.

export type AbaSstIa = 'perguntar' | 'chamado' | 'chamados';

const VERDE_ESCURO = '#065f46';
const VINHO = '#670000';
const BEGE = '#ebe9ad';

const useStyles = makeStyles({
  painel: {
    position: 'fixed',
    right: '20px',
    bottom: 'calc(20px + var(--sst-ia-rodape, 0px))',
    width: '400px',
    height: 'min(600px, calc(100dvh - 96px - var(--sst-ia-rodape, 0px)))',
    zIndex: 21,
    display: 'flex',
    flexDirection: 'column',
    overflow: 'hidden',
    borderRadius: '14px',
    backgroundColor: '#ffffff',
    boxShadow: '0 14px 40px rgba(0,0,0,0.25)',
    '@media (max-width: 680px)': { inset: 0, width: 'auto', height: 'auto', borderRadius: 0 },
  },
  esquerda: { right: 'auto', left: '76px', '@media (max-width: 680px)': { left: 0 } },
  oculto: { display: 'none' },
  topo: { padding: '12px 14px 0', borderRadius: 0, borderLeft: 'none', borderRight: 'none', borderTop: 'none' },
  topoLinha: { display: 'flex', alignItems: 'center', gap: '10px' },
  avatar: {
    width: '36px',
    height: '36px',
    flexShrink: 0,
    borderRadius: '50%',
    display: 'grid',
    placeItems: 'center',
    fontSize: '22px',
    color: BEGE,
    backgroundColor: 'rgba(255,255,255,0.18)',
    border: '1px solid rgba(255,255,255,0.3)',
  },
  titulo: { margin: 0, fontSize: '15px', fontWeight: 700, textShadow: '0 1px 2px rgba(6,78,59,0.35)' },
  subtitulo: { fontSize: '12px', fontWeight: 600, color: 'rgba(255,255,255,0.92)' },
  acoesTopo: { marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: '6px' },
  botaoTopo: {
    display: 'inline-flex',
    alignItems: 'center',
    gap: '4px',
    border: '1px solid rgba(255,255,255,0.3)',
    backgroundColor: 'rgba(255,255,255,0.16)',
    color: '#ffffff',
    borderRadius: '14px',
    padding: '3px 9px',
    fontSize: '11.5px',
    fontWeight: 700,
    cursor: 'pointer',
    whiteSpace: 'nowrap',
    ':hover': { backgroundColor: 'rgba(255,255,255,0.26)' },
    ':focus-visible': { outline: '2px solid #ffffff', outlineOffset: '2px' },
  },
  vozLigada: { backgroundColor: BEGE, color: '#064e3b', border: `1px solid ${BEGE}`, ':hover': { backgroundColor: '#f3f1c4' } },
  fechar: {
    width: '30px',
    height: '30px',
    padding: 0,
    justifyContent: 'center',
    borderRadius: '50%',
    '@media (max-width: 680px)': { width: '40px', height: '40px' },
  },
  abas: { display: 'flex', marginTop: '10px' },
  aba: {
    border: 'none',
    borderBottom: '3px solid transparent',
    background: 'none',
    color: 'rgba(255,255,255,0.82)',
    padding: '8px 12px',
    fontSize: '13px',
    fontWeight: 600,
    cursor: 'pointer',
    ':hover': { color: '#ffffff' },
    ':focus-visible': { outline: '2px solid #ffffff', outlineOffset: '-2px' },
  },
  abaAtiva: { color: '#ffffff', borderBottomColor: BEGE },
  corpo: {
    flex: 1,
    overflowY: 'auto',
    padding: '14px',
    backgroundColor: '#faf9f8',
    display: 'flex',
    flexDirection: 'column',
    gap: '10px',
    fontSize: '13px',
  },
  msgUsuario: {
    alignSelf: 'flex-end',
    maxWidth: '85%',
    backgroundColor: VERDE_ESCURO,
    color: '#ffffff',
    padding: '9px 12px',
    borderRadius: '12px 12px 4px 12px',
    whiteSpace: 'pre-wrap',
    overflowWrap: 'anywhere',
  },
  falado: { display: 'block', fontSize: '10.5px', opacity: 0.8, marginBottom: '2px' },
  msgIa: {
    alignSelf: 'flex-start',
    maxWidth: '90%',
    backgroundColor: '#ffffff',
    border: '1px solid #e1dfdd',
    padding: '9px 12px',
    borderRadius: '12px 12px 12px 4px',
    lineHeight: 1.5,
    textAlign: 'justify',
    overflowWrap: 'anywhere',
    '& strong': { color: VERDE_ESCURO },
  },
  msgFalando: { border: `1px solid ${VERDE_ESCURO}`, boxShadow: '0 0 0 2px rgba(6,95,70,0.12)' },
  msgErro: { border: '1px solid #f1bbbc', backgroundColor: '#fdf3f4', color: '#751d1f' },
  acoesMsg: { display: 'flex', alignItems: 'center', gap: '8px', marginTop: '6px' },
  mini: {
    display: 'inline-flex',
    alignItems: 'center',
    gap: '4px',
    border: '1px solid #a7f3d0',
    backgroundColor: '#ffffff',
    color: VERDE_ESCURO,
    borderRadius: '12px',
    padding: '2px 9px',
    fontSize: '11.5px',
    fontWeight: 600,
    cursor: 'pointer',
    ':hover': { backgroundColor: '#ecfdf5' },
  },
  status: { alignSelf: 'flex-start', fontSize: '12px', color: VERDE_ESCURO, fontWeight: 600, display: 'flex', gap: '8px', alignItems: 'center' },
  boasVindas: { color: '#323130', lineHeight: 1.5 },
  sugestoes: { display: 'flex', flexWrap: 'wrap', gap: '6px', marginTop: '4px' },
  sugestao: {
    border: '1px solid #e1dfdd',
    backgroundColor: '#ffffff',
    borderRadius: '14px',
    padding: '4px 10px',
    fontSize: '12px',
    color: '#484644',
    cursor: 'pointer',
    ':hover': { border: '1px solid #10b981', color: VERDE_ESCURO },
  },
  oferta: {
    alignSelf: 'flex-start',
    backgroundColor: '#f7f6e3',
    border: `1px solid ${BEGE}`,
    borderRadius: '10px',
    padding: '10px 12px',
    fontSize: '12.5px',
    color: '#4a4a2a',
  },
  entrada: { borderTop: '1px solid #e1dfdd', padding: '10px', display: 'flex', gap: '8px', alignItems: 'flex-end', backgroundColor: '#ffffff' },
  campo: { flex: 1 },
  aviso: { fontSize: '10.5px', color: '#605e5c', textAlign: 'center', padding: '0 10px 6px', backgroundColor: '#ffffff' },
  rodape: {
    borderTop: '1px solid #e1dfdd',
    padding: '6px 12px',
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    backgroundColor: '#ffffff',
  },
  link: {
    border: 'none',
    background: 'none',
    color: '#605e5c',
    fontSize: '12px',
    fontWeight: 600,
    cursor: 'pointer',
    textDecoration: 'underline',
    padding: '4px 0',
    ':hover': { color: VERDE_ESCURO },
  },
  form: { display: 'flex', flexDirection: 'column', gap: '10px' },
  linha2: { display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px' },
  rotulo: { display: 'flex', flexDirection: 'column', gap: '3px', fontSize: '12px', fontWeight: 600, color: '#484644' },
  preenchidoIa: { '& input, & select, & textarea': { backgroundColor: '#f7f6e3' } },
  dica: { fontSize: '11.5px', color: '#605e5c' },
  item: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    gap: '8px',
    backgroundColor: '#ffffff',
    border: '1px solid #e1dfdd',
    borderRadius: '8px',
    padding: '10px 12px',
    cursor: 'pointer',
    textAlign: 'left',
    fontSize: '12.5px',
    ':hover': { border: '1px solid #10b981' },
  },
  itemTitulo: { overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' },
  selo: { flexShrink: 0, fontSize: '11px', fontWeight: 600, borderRadius: '10px', padding: '2px 8px', backgroundColor: '#eef0ff', color: '#2b3a99' },
  seloNovidade: { backgroundColor: VINHO, color: '#ffffff' },
  seloFeito: { backgroundColor: '#dff6dd', color: '#0e5c0e' },
});

const STATUS_FECHADOS = [5, 6];

const SUGESTOES = ['Como aprovo o PGR?', 'O que bloqueia "Aptos para o campo"?', 'Como registro uma entrega de EPI?'];

export function PainelSstIa({
  aba,
  setAba,
  conversa,
  leitura,
  vozLigada,
  setVozLigada,
  aoFechar,
  aoEsconderHoje,
  aoMudarChamados,
  chamados,
  esquerda,
  oculto,
}: {
  aba: AbaSstIa;
  setAba: (a: AbaSstIa) => void;
  conversa: ReturnType<typeof useConversaSstIa>;
  leitura: ReturnType<typeof useLeituraVoz>;
  vozLigada: boolean;
  setVozLigada: (v: boolean) => void;
  aoFechar: () => void;
  aoEsconderHoje: () => void;
  aoMudarChamados: () => void;
  chamados: SuporteIaSolicitacao[] | null;
  esquerda: boolean;
  /** Janela modal na frente: o painel some sem desmontar (a conversa e o formulário ficam). */
  oculto: boolean;
}) {
  const estilos = useStyles();
  const esm = useEstilosEsmeralda();
  const navigate = useNavigate();
  const location = useLocation();
  const [texto, setTexto] = useState('');
  const [erroVoz, setErroVoz] = useState<string | null>(null);
  const fimRef = useRef<HTMLDivElement | null>(null);
  const campoRef = useRef<HTMLTextAreaElement | null>(null);

  // Tela atual vai junto como contexto (só o caminho, nunca dados da tela).
  const tela = `${document.title || 'G-SST'} (${location.pathname})`;

  useEffect(() => {
    fimRef.current?.scrollIntoView({ block: 'end' });
  }, [conversa.mensagens, conversa.status]);

  useEffect(() => {
    if (aba === 'perguntar') campoRef.current?.focus();
  }, [aba]);

  const voz = useGravacaoVoz(
    api.agenteIa.transcrever,
    (r) => {
      setErroVoz(null);
      if (r.transcricao.trim()) void conversa.enviar(r.transcricao, tela, true);
      else setErroVoz('Não deu para entender a fala. Tente de novo ou escreva a pergunta.');
    },
    (m) => setErroVoz(m),
  );

  const enviarTexto = () => {
    if (!texto.trim()) return;
    void conversa.enviar(texto, tela, false);
    setTexto('');
  };

  const aoTeclar = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      enviarTexto();
    }
  };

  // ----- Abrir chamado -----
  const [tipo, setTipo] = useState<number>(TipoSolicitacaoSuporteIa.Duvida);
  const [severidade, setSeveridade] = useState<number>(SeveridadeSolicitacaoSuporteIa.Media);
  const [titulo, setTitulo] = useState('');
  const [modulo, setModulo] = useState('');
  const [descricao, setDescricao] = useState('');
  const [preenchidoIa, setPreenchidoIa] = useState(false);
  const [preenchendo, setPreenchendo] = useState(false);
  const [enviandoChamado, setEnviandoChamado] = useState(false);
  const [erroChamado, setErroChamado] = useState<string | null>(null);
  const [chamadoAberto, setChamadoAberto] = useState<string | null>(null);

  const conversaComoTexto = useCallback(
    () =>
      conversa.mensagens
        .filter((m) => !m.erro && m.texto.trim())
        .map((m) => `${m.autor === 'usuario' ? 'Usuário' : 'SST IA'}: ${m.texto.trim()}`)
        .join('\n\n'),
    [conversa.mensagens],
  );

  const abrirChamadoDaConversa = async () => {
    setAba('chamado');
    setChamadoAberto(null);
    setErroChamado(null);
    const relato = conversaComoTexto();
    if (!relato) return;
    setPreenchendo(true);
    try {
      const r = await api.suporteIa.relatoTexto(`Tela: ${location.pathname}\n\n${relato}`.slice(0, 8000));
      if (r.sugestao) {
        setTipo(r.sugestao.tipo);
        setSeveridade(r.sugestao.severidade);
        setTitulo(r.sugestao.titulo);
        setModulo(r.sugestao.modulo ?? '');
        setDescricao(r.sugestao.descricao);
      } else {
        setDescricao(relato);
      }
      setPreenchidoIa(true);
    } catch {
      setDescricao(relato);
      setPreenchidoIa(true);
    } finally {
      setPreenchendo(false);
    }
  };

  const enviarChamado = async () => {
    setEnviandoChamado(true);
    setErroChamado(null);
    try {
      const criado = await api.suporteIa.criar({
        tipo,
        severidadeInformada: severidade,
        titulo: titulo.trim(),
        descricao: descricao.trim(),
        modulo: modulo.trim() || null,
        urlContexto: window.location.href,
      });
      setChamadoAberto(criado.titulo);
      setTitulo('');
      setDescricao('');
      setModulo('');
      setPreenchidoIa(false);
      aoMudarChamados();
    } catch (e) {
      setErroChamado(e instanceof Error ? e.message : 'Não foi possível abrir o chamado.');
    } finally {
      setEnviandoChamado(false);
    }
  };

  const respondeuAlgo = conversa.mensagens.some((m) => m.autor === 'ia' && !m.gerando && !m.erro);
  const gravando = voz.estado === 'gravando';
  const transcrevendo = voz.estado === 'transcrevendo';

  function botaoOuvir(m: MensagemSstIa) {
    if (!leitura.suportado || m.gerando || m.erro || !m.texto) return null;
    const lendo = leitura.lendoId === m.id;
    return (
      <div className={estilos.acoesMsg}>
        <button type="button" className={estilos.mini} onClick={() => (lendo ? leitura.parar() : leitura.ler(m.id, m.texto))}>
          {lendo ? <Stop16Filled /> : <Speaker220Regular style={{ fontSize: '14px' }} />}
          {lendo ? 'Parar' : 'Ouvir'}
        </button>
      </div>
    );
  }

  return (
    <section
      className={mergeClasses(estilos.painel, esquerda && estilos.esquerda, oculto && estilos.oculto)}
      aria-hidden={oculto || undefined}
      role="region"
      aria-label="SST IA"
      data-sst-ia-painel=""
      onKeyDown={(e) => {
        if (e.key === 'Escape') aoFechar();
      }}
    >
      <header className={mergeClasses(esm.vidro, estilos.topo)}>
        <div className={estilos.topoLinha}>
          <div className={estilos.avatar}>
            <IconeSstIa />
          </div>
          <div>
            <h2 className={estilos.titulo}>SST IA</h2>
            <div className={estilos.subtitulo}>Tire dúvidas do G-SST ou abra um chamado</div>
          </div>
          <div className={estilos.acoesTopo}>
            {leitura.suportado && (
              <button
                type="button"
                className={mergeClasses(estilos.botaoTopo, vozLigada && estilos.vozLigada)}
                aria-pressed={vozLigada}
                title={vozLigada ? 'Respostas a perguntas faladas são lidas em voz alta' : 'Leitura em voz alta desligada'}
                onClick={() => {
                  if (vozLigada) leitura.parar();
                  setVozLigada(!vozLigada);
                }}
              >
                {vozLigada ? <Speaker220Regular style={{ fontSize: '14px' }} /> : <SpeakerMute20Regular style={{ fontSize: '14px' }} />}
                {vozLigada ? 'Voz' : 'Mudo'}
              </button>
            )}
            <button type="button" className={mergeClasses(estilos.botaoTopo, estilos.fechar)} onClick={aoFechar} aria-label="Fechar o SST IA" title="Fechar (Esc)">
              <Dismiss20Regular />
            </button>
          </div>
        </div>
        <nav className={estilos.abas} aria-label="Seções do SST IA">
          {(
            [
              ['perguntar', 'Perguntar'],
              ['chamado', 'Abrir chamado'],
              ['chamados', 'Meus chamados'],
            ] as const
          ).map(([id, rotulo]) => (
            <button
              key={id}
              type="button"
              className={mergeClasses(estilos.aba, aba === id && estilos.abaAtiva)}
              aria-current={aba === id ? 'page' : undefined}
              onClick={() => setAba(id)}
            >
              {rotulo}
            </button>
          ))}
        </nav>
      </header>

      {aba === 'perguntar' && (
        <>
          <div className={estilos.corpo} aria-live="polite">
            {conversa.mensagens.length === 0 && (
              <div className={estilos.boasVindas}>
                Olá! Pergunte como usar qualquer tela do G-SST, o que significa uma mensagem de erro ou o que a NR pede.
                Você pode escrever ou tocar no microfone e falar.
                <div className={estilos.sugestoes}>
                  {SUGESTOES.map((s) => (
                    <button key={s} type="button" className={estilos.sugestao} onClick={() => void conversa.enviar(s, tela)}>
                      {s}
                    </button>
                  ))}
                </div>
              </div>
            )}
            {conversa.mensagens.map((m) =>
              m.autor === 'usuario' ? (
                <div key={m.id} className={estilos.msgUsuario}>
                  {m.falada && <span className={estilos.falado}>🎤 falado</span>}
                  {m.texto}
                </div>
              ) : m.gerando && !m.texto ? null : (
                <div
                  key={m.id}
                  className={mergeClasses(estilos.msgIa, leitura.lendoId === m.id && estilos.msgFalando, m.erro && estilos.msgErro)}
                >
                  <TextoSstIa texto={m.texto} />
                  {botaoOuvir(m)}
                </div>
              ),
            )}
            {conversa.status && (
              <div className={estilos.status} role="status">
                <Spinner size="extra-tiny" />
                {conversa.status}
              </div>
            )}
            {respondeuAlgo && !conversa.respondendo && (
              <div className={estilos.oferta}>
                Não resolveu ou é um defeito do sistema?{' '}
                <button type="button" className={estilos.link} style={{ color: VINHO }} onClick={() => void abrirChamadoDaConversa()}>
                  Abrir chamado com esta conversa
                </button>
              </div>
            )}
            {erroVoz && <FeedbackInline tom="erro">{erroVoz}</FeedbackInline>}
            <div ref={fimRef} />
          </div>
          <div className={estilos.entrada}>
            {gravando ? (
              <div className={estilos.campo}>
                <IndicadorGravacao segundos={voz.segundos} niveis={voz.niveis} />
              </div>
            ) : (
              <Textarea
                ref={campoRef}
                className={estilos.campo}
                resize="none"
                rows={1}
                value={transcrevendo ? 'Transcrevendo a sua fala…' : texto}
                disabled={transcrevendo}
                maxLength={4000}
                placeholder="Escreva sua dúvida…"
                aria-label="Pergunta para o SST IA"
                onChange={(_, d) => setTexto(d.value)}
                onKeyDown={aoTeclar}
              />
            )}
            <Button
              appearance="subtle"
              shape="circular"
              icon={gravando ? <RecordStop20Filled color={VINHO} /> : <Mic20Regular />}
              aria-label={gravando ? 'Parar e enviar a pergunta falada' : 'Perguntar falando'}
              title={gravando ? 'Parar e enviar' : 'Perguntar falando'}
              disabled={conversa.respondendo || transcrevendo}
              onClick={() => (gravando ? voz.parar() : void voz.iniciar())}
            />
            {conversa.respondendo ? (
              <Button appearance="subtle" shape="circular" icon={<Stop16Filled />} aria-label="Interromper a resposta" onClick={conversa.interromper} />
            ) : (
              <Button
                appearance="primary"
                shape="circular"
                icon={<Send20Filled />}
                aria-label="Enviar pergunta"
                disabled={!texto.trim() || gravando || transcrevendo}
                onClick={enviarTexto}
              />
            )}
          </div>
          <div className={estilos.aviso}>Não escreva CPF nem dados de saúde ou de ASO. O SST IA não vê nem altera seus dados.</div>
        </>
      )}

      {aba === 'chamado' && (
        <div className={estilos.corpo}>
          {preenchendo ? (
            <div className={estilos.status}>
              <Spinner size="extra-tiny" /> Preenchendo o chamado com a conversa…
            </div>
          ) : chamadoAberto ? (
            <>
              <FeedbackInline tom="sucesso">Chamado aberto: "{chamadoAberto}". Acompanhe em Meus chamados.</FeedbackInline>
              <Button appearance="secondary" onClick={() => setAba('chamados')}>
                Ver meus chamados
              </Button>
            </>
          ) : (
            <div className={mergeClasses(estilos.form, preenchidoIa && estilos.preenchidoIa)}>
              <div className={estilos.linha2}>
                <label className={estilos.rotulo}>
                  Tipo
                  <Select value={String(tipo)} onChange={(_, d) => setTipo(Number(d.value))}>
                    {Object.entries(tipoSolicitacaoSuporteIaLabel).map(([v, r]) => (
                      <option key={v} value={v}>
                        {r}
                      </option>
                    ))}
                  </Select>
                </label>
                <label className={estilos.rotulo}>
                  Gravidade
                  <Select value={String(severidade)} onChange={(_, d) => setSeveridade(Number(d.value))}>
                    {Object.entries(severidadeSolicitacaoSuporteIaLabel).map(([v, r]) => (
                      <option key={v} value={v}>
                        {r}
                      </option>
                    ))}
                  </Select>
                </label>
              </div>
              <label className={estilos.rotulo}>
                Tela ou módulo
                <Input value={modulo} placeholder="Ex.: PGR / GRO" onChange={(_, d) => setModulo(d.value)} />
              </label>
              <label className={estilos.rotulo}>
                Título
                <Input value={titulo} maxLength={200} onChange={(_, d) => setTitulo(d.value)} />
              </label>
              <label className={estilos.rotulo}>
                O que aconteceu
                <Textarea
                  value={descricao}
                  rows={5}
                  resize="vertical"
                  placeholder="Tela, o que você fez e a mensagem exata que apareceu."
                  onChange={(_, d) => setDescricao(d.value)}
                />
              </label>
              {preenchidoIa && <span className={estilos.dica}>Campos em bege foram preenchidos pela IA. Confira antes de abrir.</span>}
              {!preenchidoIa && respondeuAlgo && (
                <button type="button" className={estilos.link} style={{ alignSelf: 'flex-start' }} onClick={() => void abrirChamadoDaConversa()}>
                  Preencher com a conversa do SST IA
                </button>
              )}
              {erroChamado && <FeedbackInline tom="erro">{erroChamado}</FeedbackInline>}
              <Button
                appearance="primary"
                disabled={enviandoChamado || !titulo.trim() || !descricao.trim()}
                onClick={() => void enviarChamado()}
              >
                {enviandoChamado ? 'Abrindo…' : 'Abrir chamado'}
              </Button>
            </div>
          )}
        </div>
      )}

      {aba === 'chamados' && (
        <div className={estilos.corpo}>
          {chamados === null ? (
            <div className={estilos.status}>
              <Spinner size="extra-tiny" /> Carregando…
            </div>
          ) : chamados.length === 0 ? (
            <span className={estilos.boasVindas}>Você ainda não abriu nenhum chamado.</span>
          ) : (
            chamados.slice(0, 8).map((c) => (
              <button key={c.id} type="button" className={estilos.item} onClick={() => navigate(`/suporte-ia/${c.id}`)}>
                <span className={estilos.itemTitulo}>{c.titulo}</span>
                <span
                  className={mergeClasses(
                    estilos.selo,
                    STATUS_COM_NOVIDADE.includes(c.status) && estilos.seloNovidade,
                    STATUS_FECHADOS.includes(c.status) && estilos.seloFeito,
                  )}
                >
                  {statusSolicitacaoSuporteIaLabel[c.status] ?? '—'}
                </span>
              </button>
            ))
          )}
          <button type="button" className={estilos.link} style={{ alignSelf: 'center' }} onClick={() => navigate('/suporte-ia')}>
            Ver todos os chamados →
          </button>
        </div>
      )}

      <div className={estilos.rodape}>
        {aba === 'perguntar' && conversa.mensagens.length > 0 ? (
          <button type="button" className={estilos.link} onClick={conversa.novaConversa}>
            Nova conversa
          </button>
        ) : (
          <span className={estilos.dica}>Também no menu lateral</span>
        )}
        <button type="button" className={estilos.link} onClick={aoEsconderHoje}>
          Esconder por hoje
        </button>
      </div>
    </section>
  );
}

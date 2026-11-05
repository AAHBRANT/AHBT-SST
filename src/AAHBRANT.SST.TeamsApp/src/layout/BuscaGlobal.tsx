import { useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button, Dialog, DialogSurface, Spinner, makeStyles, mergeClasses } from '@fluentui/react-components';
import { Search20Regular } from '@fluentui/react-icons';
import { api } from '../lib/api';
import { designTokens } from '../ui/tokens/tokens';
import { modulosNav, rotaDaSecao } from './mapaNavegacao';

// Busca global Ctrl+K (melhoria 1 da proposta de navegação, 03/11/2026): pessoas, obras, APRs, NCs e
// telas num campo só. Os dados vêm das listas que a API já devolve para o usuário — o escopo por obra
// e perfil é o mesmo das telas, a busca não enxerga nada além. Carrega só ao abrir, uma vez por abertura.

interface Resultado {
  grupo: 'Telas' | 'Pessoas' | 'Obras' | 'Documentos';
  titulo: string;
  detalhe?: string;
  para: string;
  // Texto em que se procura (inclui CPF sem pontuação, que nunca é mostrado inteiro).
  alvo: string;
}

const LIMITE_POR_GRUPO = 5;
const ORDEM_GRUPOS: Resultado['grupo'][] = ['Telas', 'Pessoas', 'Documentos', 'Obras'];

const normalizar = (texto: string) =>
  texto.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();

// LGPD: só os 3 primeiros dígitos do CPF aparecem (mesma máscara usada no restante do app).
const mascararCpf = (cpf: string) => {
  const digitos = cpf.replace(/\D/g, '');
  return digitos.length === 11 ? `CPF ${digitos.slice(0, 3)}.***.***-**` : undefined;
};

function telasFixas(): Resultado[] {
  const telas: Resultado[] = [];
  for (const modulo of Object.values(modulosNav)) {
    telas.push({ grupo: 'Telas', titulo: modulo.rotulo, para: modulo.rota, alvo: normalizar(modulo.rotulo) });
    for (const secao of modulo.secoes ?? []) {
      telas.push({
        grupo: 'Telas',
        titulo: secao.rotulo,
        detalhe: modulo.rotulo,
        para: rotaDaSecao(modulo, secao),
        alvo: normalizar(`${secao.rotulo} ${modulo.rotulo}`),
      });
    }
  }
  telas.push({ grupo: 'Telas', titulo: 'Calendário', para: '/calendario', alvo: 'calendario' });
  telas.push({ grupo: 'Telas', titulo: 'Administração', para: '/administracao', alvo: 'administracao' });
  return telas;
}

const useStyles = makeStyles({
  gatilho: {
    minWidth: '220px',
    justifyContent: 'flex-start',
    color: designTokens.colorNeutralMedium,
    fontWeight: 400,
    '@media (max-width: 1100px)': { minWidth: 'auto' },
  },
  gatilhoTexto: { '@media (max-width: 1100px)': { display: 'none' } },
  tecla: {
    marginLeft: 'auto',
    paddingLeft: '12px',
    fontSize: '11px',
    '@media (max-width: 1100px)': { display: 'none' },
  },
  superficie: { padding: 0, maxWidth: '560px', width: 'calc(100vw - 32px)', overflow: 'hidden' },
  campo: {
    width: '100%',
    boxSizing: 'border-box',
    border: 0,
    borderBottom: `1px solid ${designTokens.colorCardBorder}`,
    padding: '14px 16px',
    fontSize: '15px',
    background: 'transparent',
    color: 'inherit',
    outline: 'none',
    ':focus-visible': { boxShadow: 'inset 0 -2px 0 var(--sst-color-primary)' },
  },
  lista: { maxHeight: '60vh', overflowY: 'auto', padding: '4px 0' },
  grupo: {
    fontSize: '11px',
    letterSpacing: '0.08em',
    textTransform: 'uppercase',
    color: designTokens.colorNeutralMedium,
    padding: '10px 16px 4px',
  },
  item: {
    display: 'flex',
    alignItems: 'baseline',
    gap: '10px',
    padding: '8px 16px',
    cursor: 'pointer',
    fontSize: '14px',
  },
  itemFoco: { backgroundColor: 'color-mix(in srgb, var(--sst-color-primary) 10%, transparent)' },
  detalhe: { color: designTokens.colorNeutralMedium, fontSize: '13px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' },
  vazio: { padding: '16px', color: designTokens.colorNeutralMedium, fontSize: '14px' },
  rodape: {
    display: 'flex',
    gap: '16px',
    flexWrap: 'wrap',
    padding: '8px 16px',
    fontSize: '12px',
    color: designTokens.colorNeutralMedium,
    borderTop: `1px solid ${designTokens.colorCardBorder}`,
  },
});

export function BuscaGlobal() {
  const estilos = useStyles();
  const navigate = useNavigate();
  const [aberta, setAberta] = useState(false);
  const [termo, setTermo] = useState('');
  const [foco, setFoco] = useState(0);
  const [dados, setDados] = useState<Resultado[]>([]);
  const [carregando, setCarregando] = useState(false);
  const campo = useRef<HTMLInputElement>(null);
  const telas = useMemo(telasFixas, []);

  useEffect(() => {
    const aoTeclar = (evento: KeyboardEvent) => {
      if ((evento.ctrlKey || evento.metaKey) && evento.key.toLowerCase() === 'k') {
        evento.preventDefault();
        setAberta(true);
      }
    };
    window.addEventListener('keydown', aoTeclar);
    return () => window.removeEventListener('keydown', aoTeclar);
  }, []);

  useEffect(() => {
    if (!aberta) return;
    setTermo('');
    setFoco(0);
    let cancelado = false;
    setCarregando(true);
    // Cada lista falha sozinha: sem permissão para APR, por exemplo, a busca segue com o resto.
    const seguro = <T,>(promessa: Promise<T[]>) => promessa.catch(() => [] as T[]);
    Promise.all([
      seguro(api.trabalhadores.listar()),
      seguro(api.obras.listar()),
      seguro(api.aprs.listar()),
      seguro(api.naoConformidades.listar()),
    ]).then(([trabalhadores, obras, aprs, ncs]) => {
      if (cancelado) return;
      const obraPorId = new Map(obras.map((o) => [o.id, o.nome]));
      setDados([
        ...trabalhadores.map((t): Resultado => ({
          grupo: 'Pessoas',
          titulo: t.nome,
          detalhe: [t.matricula, obraPorId.get(t.obraId), mascararCpf(t.cpf ?? '')].filter(Boolean).join(' · '),
          para: `/pessoas/${t.id}`,
          alvo: normalizar(`${t.nome} ${t.matricula ?? ''} ${(t.cpf ?? '').replace(/\D/g, '')}`),
        })),
        ...obras.map((o): Resultado => ({
          grupo: 'Obras',
          titulo: o.nome,
          detalhe: o.codigo,
          para: '/administracao?aba=obras',
          alvo: normalizar(`${o.nome} ${o.codigo}`),
        })),
        ...aprs.map((a): Resultado => ({
          grupo: 'Documentos',
          titulo: a.numeroApr ? `APR ${a.numeroApr}` : 'APR',
          detalhe: [a.atividadeNome, a.obraNome].filter(Boolean).join(' · '),
          para: `/operacao/apr/${a.id}`,
          alvo: normalizar(`apr ${a.numeroApr ?? ''} ${a.atividadeNome} ${a.local}`),
        })),
        ...ncs.map((n): Resultado => ({
          grupo: 'Documentos',
          titulo: 'Não conformidade',
          detalhe: [n.descricao, n.obraNome].filter(Boolean).join(' · '),
          para: `/nao-conformidades/${n.id}`,
          alvo: normalizar(`nc nao conformidade ${n.descricao} ${n.local ?? ''}`),
        })),
      ]);
      setCarregando(false);
    });
    return () => {
      cancelado = true;
    };
  }, [aberta]);

  const resultados = useMemo(() => {
    const busca = normalizar(termo.trim());
    if (!busca) return telas.filter((t) => !t.detalhe).slice(0, 6);
    const palavras = busca.split(/\s+/);
    const casam = (r: Resultado) => palavras.every((p) => r.alvo.includes(p));
    const todos = [...telas, ...dados].filter(casam);
    return ORDEM_GRUPOS.flatMap((g) => todos.filter((r) => r.grupo === g).slice(0, LIMITE_POR_GRUPO));
  }, [termo, telas, dados]);

  const abrir = (resultado: Resultado | undefined) => {
    if (!resultado) return;
    setAberta(false);
    navigate(resultado.para);
  };

  const aoTeclarCampo = (evento: React.KeyboardEvent) => {
    if (evento.key === 'ArrowDown') {
      evento.preventDefault();
      setFoco((f) => (resultados.length ? (f + 1) % resultados.length : 0));
    } else if (evento.key === 'ArrowUp') {
      evento.preventDefault();
      setFoco((f) => (resultados.length ? (f - 1 + resultados.length) % resultados.length : 0));
    } else if (evento.key === 'Enter') {
      evento.preventDefault();
      abrir(resultados[foco]);
    }
  };

  let grupoAnterior = '';
  return (
    <>
      <Button
        className={estilos.gatilho}
        appearance="outline"
        size="small"
        icon={<Search20Regular />}
        onClick={() => setAberta(true)}
        aria-label="Buscar (Ctrl+K)"
        title="Buscar (Ctrl+K)"
      >
        <span className={estilos.gatilhoTexto}>Buscar pessoa, documento ou tela</span>
        <kbd className={estilos.tecla}>Ctrl K</kbd>
      </Button>
      <Dialog open={aberta} onOpenChange={(_, d) => setAberta(d.open)}>
        <DialogSurface className={estilos.superficie} aria-label="Busca global">
          <input
            id="busca-global"
            ref={campo}
            autoFocus
            className={estilos.campo}
            placeholder="Nome, matrícula, CPF, APR, NC ou nome da tela…"
            value={termo}
            onChange={(e) => {
              setTermo(e.target.value);
              setFoco(0);
            }}
            onKeyDown={aoTeclarCampo}
            autoComplete="off"
            role="combobox"
            aria-expanded="true"
            aria-controls="busca-global-lista"
          />
          <div id="busca-global-lista" className={estilos.lista} role="listbox">
            {resultados.length === 0 && (
              <div className={estilos.vazio}>
                {carregando ? <Spinner size="tiny" label="Carregando…" /> : `Nada encontrado para "${termo}". Tente o nome, a matrícula ou o número da APR.`}
              </div>
            )}
            {resultados.map((r, i) => {
              const cabecalho = r.grupo !== grupoAnterior ? (termo.trim() ? r.grupo : 'Ir para') : null;
              grupoAnterior = r.grupo;
              return (
                <div key={`${r.grupo}-${r.para}-${i}`}>
                  {cabecalho && <div className={estilos.grupo}>{cabecalho}</div>}
                  <div
                    role="option"
                    aria-selected={i === foco}
                    className={mergeClasses(estilos.item, i === foco && estilos.itemFoco)}
                    onMouseMove={() => i !== foco && setFoco(i)}
                    onClick={() => abrir(r)}
                  >
                    <span>{r.titulo}</span>
                    {r.detalhe && <span className={estilos.detalhe}>{r.detalhe}</span>}
                  </div>
                </div>
              );
            })}
            {carregando && resultados.length > 0 && <div className={estilos.vazio}>Carregando pessoas e documentos…</div>}
          </div>
          <div className={estilos.rodape}>
            <span>↑ ↓ navegar</span>
            <span>Enter abrir</span>
            <span>Esc fechar</span>
          </div>
        </DialogSurface>
      </Dialog>
    </>
  );
}

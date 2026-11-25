import { Fragment, useEffect, useMemo, useRef, useState, type KeyboardEvent, type ReactNode } from 'react';
import { Input, makeStyles, mergeClasses, shorthands } from '@fluentui/react-components';
import { Checkmark12Filled, Dismiss12Regular, Search20Regular } from '@fluentui/react-icons';
import { designTokens, tokensUi } from '../../tokens/tokens';

// Seleção múltipla com busca (pedido do usuário 29/09: chips não escalam para ~200 funcionários).
// Redesenho aprovado em 25/11/2026 (docs/mockups/seletor-pessoas-2026-11-25.html):
// - os escolhidos ficam numa faixa no topo, com miniatura e × para tirar (até 8 + "+N");
// - uma coluna, opcionalmente agrupada por letra com a letra fixa no topo da rolagem;
// - caixa de marcar quadrada (é seleção de várias), teclado ↑ ↓ + Espaço;
// - filtros de um toque com contagem (`filtros`);
// - "Marcar os N" só com busca/filtro mudado ou lista curta, para não marcar 200 por engano.
// Busca ignora acentos/caixa; selecionados que saem do filtro continuam selecionados.

const MAX_CHIPS = 8;
const LISTA_CURTA = 30;

const useStyles = makeStyles({
  raiz: {
    display: 'flex', flexDirection: 'column', width: '100%', minWidth: 0,
    ...shorthands.border('1px', 'solid', designTokens.colorCardBorder),
    ...shorthands.borderRadius(tokensUi.raio.lg), backgroundColor: designTokens.colorSurface, overflow: 'hidden',
  },
  escolhidos: {
    display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: '6px', minHeight: '48px',
    ...shorthands.padding(tokensUi.espaco.sm, tokensUi.espaco.md),
    backgroundColor: `color-mix(in srgb, ${designTokens.colorSurface} 92%, ${designTokens.colorPrimary})`,
    borderBottom: `1px solid ${designTokens.colorCardBorder}`,
  },
  escolhidosTitulo: { fontSize: '12.5px', fontWeight: 700, color: designTokens.colorPrimary, marginRight: '4px' },
  escolhidosVazio: { fontSize: '12.5px', color: designTokens.colorNeutralMedium },
  chip: {
    display: 'inline-flex', alignItems: 'center', gap: '6px', maxWidth: '260px',
    ...shorthands.padding('3px', '3px', '3px', '3px'), ...shorthands.borderRadius(tokensUi.raio.full),
    backgroundColor: designTokens.colorSurface, ...shorthands.border('1px', 'solid', designTokens.colorCardBorder),
    fontSize: '12.5px', fontWeight: 500, color: designTokens.colorNeutralDark,
    animationName: { from: { opacity: 0, transform: 'scale(0.85)' }, to: { opacity: 1, transform: 'none' } },
    animationDuration: '280ms', animationTimingFunction: 'cubic-bezier(0.2, 0.9, 0.25, 1.3)',
  },
  chipSemMiniatura: { paddingLeft: '10px' },
  chipTexto: { overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' },
  chipTirar: {
    border: 'none', background: 'none', cursor: 'pointer', width: '20px', height: '20px', display: 'grid', placeItems: 'center',
    ...shorthands.borderRadius(tokensUi.raio.full), color: designTokens.colorNeutralMedium, flexShrink: 0,
    ':hover': { backgroundColor: designTokens.colorNeutralLight, color: tokensUi.status.alerta.tinta },
    ':focus-visible': { outline: `2px solid ${tokensUi.chrome.ativoFundo}` },
  },
  linkAcao: {
    border: 'none', background: 'none', cursor: 'pointer', fontFamily: 'inherit', fontSize: '12.5px', fontWeight: 500,
    color: designTokens.colorPrimary, textDecorationLine: 'underline', textUnderlineOffset: '2px',
    ...shorthands.padding('4px', '6px'), ...shorthands.borderRadius(tokensUi.raio.sm),
    ':hover': { backgroundColor: designTokens.colorSurface },
    ':disabled': { color: designTokens.colorNeutralMedium, cursor: 'default', textDecorationLine: 'none' },
  },
  acoesEscolhidos: { marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: '4px', flexWrap: 'wrap' },
  cabecalho: {
    display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.sm,
    ...shorthands.padding(tokensUi.espaco.sm, tokensUi.espaco.md), borderBottom: `1px solid ${designTokens.colorCardBorder}`,
  },
  busca: { width: '100%' },
  filtros: { display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: '6px' },
  filtrosRotulo: { fontSize: '10.5px', fontWeight: 700, letterSpacing: '0.07em', textTransform: 'uppercase', color: designTokens.colorNeutralMedium, marginRight: '2px' },
  filtro: {
    display: 'inline-flex', alignItems: 'center', gap: '6px', cursor: 'pointer', fontFamily: 'inherit', fontSize: '12.5px', fontWeight: 500,
    ...shorthands.padding('5px', '11px'), ...shorthands.borderRadius(tokensUi.raio.full),
    ...shorthands.border('1px', 'solid', tokensUi.bordaSuave), backgroundColor: designTokens.colorSurface, color: designTokens.colorNeutralDark,
    ':focus-visible': { outline: `2px solid ${tokensUi.chrome.ativoFundo}`, outlineOffset: '1px' },
  },
  filtroAtivo: { backgroundColor: designTokens.colorPrimary, ...shorthands.borderColor(designTokens.colorPrimary), color: designTokens.colorWhite },
  filtroContagem: { fontSize: '11px', opacity: 0.7, fontVariantNumeric: 'tabular-nums' },
  lista: { maxHeight: '330px', overflowY: 'auto', position: 'relative' },
  letra: {
    position: 'sticky', top: 0, zIndex: 1, fontSize: '11px', fontWeight: 700, letterSpacing: '0.07em', color: designTokens.colorNeutralMedium,
    ...shorthands.padding('5px', tokensUi.espaco.md), backgroundColor: designTokens.colorSurface, borderBottom: `1px solid ${tokensUi.bordaSuave}`,
  },
  item: {
    display: 'flex', alignItems: 'center', gap: '12px', cursor: 'pointer', outlineStyle: 'none',
    ...shorthands.padding('8px', tokensUi.espaco.md), borderBottom: `1px solid ${tokensUi.bordaSuave}`,
    transitionProperty: 'background-color', transitionDuration: tokensUi.duracao.rapido,
    ':hover': { backgroundColor: designTokens.colorNeutralLight },
    ':focus-visible': { boxShadow: `inset 0 0 0 2px ${tokensUi.chrome.ativoFundo}` },
  },
  itemMarcado: { backgroundColor: `color-mix(in srgb, ${designTokens.colorSurface} 95%, ${designTokens.colorPrimary})` },
  caixa: {
    width: '18px', height: '18px', flexShrink: 0, display: 'grid', placeItems: 'center', color: designTokens.colorWhite,
    ...shorthands.borderRadius('5px'), ...shorthands.border('1.5px', 'solid', designTokens.colorNeutralMedium),
    transitionProperty: 'background-color, border-color', transitionDuration: tokensUi.duracao.rapido,
  },
  caixaMarcada: { backgroundColor: designTokens.colorPrimary, ...shorthands.borderColor(designTokens.colorPrimary) },
  textos: { minWidth: 0, flexGrow: 1, display: 'flex', flexDirection: 'column', gap: '1px' },
  rotulo: { fontSize: '13.5px', fontWeight: 600, color: designTokens.colorNeutralDark },
  descricao: { fontSize: '11.5px', color: designTokens.colorNeutralMedium },
  rodape: {
    display: 'flex', flexWrap: 'wrap', justifyContent: 'space-between', alignItems: 'center', gap: tokensUi.espaco.sm,
    ...shorthands.padding('6px', tokensUi.espaco.md), backgroundColor: designTokens.colorNeutralLight, fontSize: '12px', color: designTokens.colorNeutralMedium,
  },
  vazio: { ...shorthands.padding(tokensUi.espaco.lg), textAlign: 'center', fontSize: '13px', color: designTokens.colorNeutralMedium },
});

export interface OpcaoSelecao {
  id: string;
  rotulo: string;
  /** Linha discreta abaixo do rótulo (ex.: "AUR-0144 · Pedreiro"). */
  descricao?: string;
  /** Visual à esquerda na lista (ex.: foto 32 px). */
  inicio?: ReactNode;
  /** Visual pequeno no chip dos escolhidos (ex.: foto 20 px). */
  miniatura?: ReactNode;
  /** Algo à direita da linha (ex.: um chip). */
  fim?: ReactNode;
  /** Texto extra considerado pela busca (ex.: matrícula e função). */
  textoBusca?: string;
}

export interface FiltroSelecao {
  id: string;
  rotulo: string;
  /** A opção passa neste filtro? */
  aceita: (opcaoId: string) => boolean;
  /** 'alternar' = liga/desliga sozinho; 'escolha' = só um filtro de escolha ativo por vez. */
  tipo?: 'alternar' | 'escolha';
  /** Começa ligado (ex.: "Só da obra"). */
  padrao?: boolean;
}

export interface ListaSelecaoMultiplaProps {
  opcoes: OpcaoSelecao[];
  selecionados: string[];
  /** Mesmo contrato do ChipCheckboxGroup: recebe função de atualização (`setState(prev => ...)`). */
  aoMudar: (atualizar: (atuais: string[]) => string[]) => void;
  placeholderBusca?: string;
  filtros?: FiltroSelecao[];
  /** Agrupa por letra inicial com a letra fixa no topo (listas de nomes). */
  agruparPorLetra?: boolean;
  /** Ações extras na faixa dos escolhidos (ex.: "Salvar como equipe"). */
  acoesEscolhidos?: ReactNode;
  /** Substantivo para a contagem ("escolhido"/"escolhidos" por padrão). */
  rotuloEscolhidos?: [singular: string, plural: string];
  'aria-label'?: string;
}

function normalizar(s: string) {
  return s.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();
}

function letraInicial(rotulo: string) {
  const c = normalizar(rotulo.trim()).charAt(0).toUpperCase();
  return /[A-Z]/.test(c) ? c : '#';
}

export function ListaSelecaoMultipla({
  opcoes, selecionados, aoMudar, placeholderBusca = 'Buscar por nome...', filtros = [], agruparPorLetra,
  acoesEscolhidos, rotuloEscolhidos = ['escolhido', 'escolhidos'], 'aria-label': ariaLabel,
}: ListaSelecaoMultiplaProps) {
  const e = useStyles();
  const listaRef = useRef<HTMLDivElement>(null);
  const [busca, setBusca] = useState('');
  const [ativos, setAtivos] = useState<Set<string>>(() => new Set(filtros.filter((f) => f.padrao).map((f) => f.id)));
  const [verTodosChips, setVerTodosChips] = useState(false);

  // Filtro que chega depois (ex.: "Só da obra" aparece quando a atividade é escolhida) entra ligado
  // se for padrão; o que a pessoa desligou à mão continua desligado.
  const vistosRef = useRef<Set<string>>(new Set(filtros.map((f) => f.id)));
  useEffect(() => {
    const novos = filtros.filter((f) => !vistosRef.current.has(f.id));
    if (novos.length === 0) return;
    novos.forEach((f) => vistosRef.current.add(f.id));
    const ligar = novos.filter((f) => f.padrao).map((f) => f.id);
    if (ligar.length) setAtivos((atual) => new Set([...atual, ...ligar]));
  }, [filtros]);

  const setSel = useMemo(() => new Set(selecionados), [selecionados]);
  const porId = useMemo(() => new Map(opcoes.map((o) => [o.id, o])), [opcoes]);

  function passa(o: OpcaoSelecao, estado: Set<string>) {
    return filtros.every((f) => !estado.has(f.id) || f.aceita(o.id));
  }
  function estadoCom(f: FiltroSelecao) {
    const novo = new Set(ativos);
    if (f.tipo === 'escolha') filtros.forEach((x) => { if (x.tipo === 'escolha') novo.delete(x.id); });
    novo.add(f.id);
    return novo;
  }
  function alternarFiltro(f: FiltroSelecao) {
    setAtivos((atual) => {
      if (atual.has(f.id)) { const n = new Set(atual); n.delete(f.id); return n; }
      const n = new Set(atual);
      if (f.tipo === 'escolha') filtros.forEach((x) => { if (x.tipo === 'escolha') n.delete(x.id); });
      n.add(f.id);
      return n;
    });
  }

  const filtradas = useMemo(() => {
    const q = normalizar(busca.trim());
    return opcoes.filter((o) => passa(o, ativos) && (!q || normalizar(`${o.rotulo} ${o.descricao ?? ''} ${o.textoBusca ?? ''}`).includes(q)));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [busca, opcoes, ativos, filtros]);

  const filtroMudado = !!busca.trim() || filtros.some((f) => ativos.has(f.id) !== !!f.padrao);
  const podeMarcarVisiveis = filtradas.length > 0 && (filtroMudado || filtradas.length <= LISTA_CURTA);
  const todosVisiveisMarcados = filtradas.length > 0 && filtradas.every((o) => setSel.has(o.id));

  function alternar(id: string) {
    aoMudar((atuais) => (atuais.includes(id) ? atuais.filter((s) => s !== id) : [...atuais, id]));
  }
  function alternarVisiveis() {
    const ids = filtradas.map((o) => o.id);
    aoMudar((atuais) => (todosVisiveisMarcados
      ? atuais.filter((s) => !ids.includes(s))
      : [...atuais, ...ids.filter((id) => !atuais.includes(id))]));
  }
  function teclaItem(ev: KeyboardEvent<HTMLDivElement>, id: string) {
    if (ev.key === ' ' || ev.key === 'Enter') { ev.preventDefault(); alternar(id); return; }
    if (ev.key === 'ArrowDown' || ev.key === 'ArrowUp') {
      ev.preventDefault();
      const itens = Array.from(listaRef.current?.querySelectorAll<HTMLElement>('[role="option"]') ?? []);
      const i = itens.indexOf(ev.currentTarget);
      itens[Math.max(0, Math.min(itens.length - 1, i + (ev.key === 'ArrowDown' ? 1 : -1)))]?.focus();
    }
  }

  const escolhidos = selecionados.map((id) => porId.get(id)).filter((o): o is OpcaoSelecao => !!o);
  const chipsVisiveis = verTodosChips ? escolhidos : escolhidos.slice(0, MAX_CHIPS);
  const escondidos = escolhidos.length - chipsVisiveis.length;

  let letraAnterior = '';
  return (
    <div className={e.raiz}>
      <div className={e.escolhidos} aria-live="polite">
        <span className={e.escolhidosTitulo}>
          {escolhidos.length} {escolhidos.length === 1 ? rotuloEscolhidos[0] : rotuloEscolhidos[1]}
        </span>
        {escolhidos.length === 0 && <span className={e.escolhidosVazio}>Ninguém ainda. Marque na lista abaixo.</span>}
        {chipsVisiveis.map((o) => (
          <span key={o.id} className={mergeClasses(e.chip, !o.miniatura && e.chipSemMiniatura)}>
            {o.miniatura}
            <span className={e.chipTexto}>{o.rotulo}</span>
            <button type="button" className={e.chipTirar} aria-label={`Tirar ${o.rotulo}`} onClick={() => alternar(o.id)}>
              <Dismiss12Regular />
            </button>
          </span>
        ))}
        {escondidos > 0 && (
          <button type="button" className={e.linkAcao} onClick={() => setVerTodosChips(true)}>+{escondidos}</button>
        )}
        {verTodosChips && escolhidos.length > MAX_CHIPS && (
          <button type="button" className={e.linkAcao} onClick={() => setVerTodosChips(false)}>Mostrar menos</button>
        )}
        {escolhidos.length > 0 && (
          <span className={e.acoesEscolhidos}>
            {acoesEscolhidos}
            <button type="button" className={e.linkAcao} onClick={() => aoMudar(() => [])}>Limpar</button>
          </span>
        )}
      </div>

      <div className={e.cabecalho}>
        <Input
          className={e.busca}
          contentBefore={<Search20Regular />}
          placeholder={placeholderBusca}
          aria-label={ariaLabel ? `Buscar em ${ariaLabel}` : 'Buscar'}
          value={busca}
          onChange={(_, d) => setBusca(d.value)}
        />
        {filtros.length > 0 && (
          <div className={e.filtros} role="group" aria-label="Filtros">
            <span className={e.filtrosRotulo}>Filtros</span>
            {filtros.map((f) => {
              const ativo = ativos.has(f.id);
              const contagem = opcoes.filter((o) => passa(o, ativo ? ativos : estadoCom(f))).length;
              return (
                <button key={f.id} type="button" aria-pressed={ativo} className={mergeClasses(e.filtro, ativo && e.filtroAtivo)} onClick={() => alternarFiltro(f)}>
                  {f.rotulo} <span className={e.filtroContagem}>{contagem}</span>
                </button>
              );
            })}
          </div>
        )}
      </div>

      <div ref={listaRef} role="listbox" aria-multiselectable="true" aria-label={ariaLabel} className={e.lista}>
        {filtradas.length === 0 ? (
          <div className={e.vazio}>{busca.trim() ? `Nenhum resultado para “${busca}”.` : 'Ninguém com esses filtros.'}</div>
        ) : filtradas.map((o) => {
          const marcado = setSel.has(o.id);
          const letra = agruparPorLetra ? letraInicial(o.rotulo) : '';
          const novaLetra = agruparPorLetra && letra !== letraAnterior;
          letraAnterior = letra;
          return (
            <Fragment key={o.id}>
              {novaLetra && <div className={e.letra} aria-hidden="true">{letra}</div>}
              <div
                role="option"
                aria-selected={marcado}
                tabIndex={0}
                className={mergeClasses(e.item, marcado && e.itemMarcado)}
                onClick={() => alternar(o.id)}
                onKeyDown={(ev) => teclaItem(ev, o.id)}
              >
                <span className={mergeClasses(e.caixa, marcado && e.caixaMarcada)} aria-hidden="true">
                  {marcado && <Checkmark12Filled />}
                </span>
                {o.inicio}
                <span className={e.textos}>
                  <span className={e.rotulo}>{o.rotulo}</span>
                  {o.descricao && <span className={e.descricao}>{o.descricao}</span>}
                </span>
                {o.fim}
              </div>
            </Fragment>
          );
        })}
      </div>

      <div className={e.rodape}>
        <span>Mostrando {filtradas.length} de {opcoes.length} · ↑ ↓ para andar, Espaço marca</span>
        {podeMarcarVisiveis && (
          <button type="button" className={e.linkAcao} onClick={alternarVisiveis}>
            {todosVisiveisMarcados ? 'Desmarcar' : 'Marcar'} {filtroMudado ? `os ${filtradas.length} filtrados` : `todos os ${filtradas.length}`}
          </button>
        )}
      </div>
    </div>
  );
}

// Estilos da peça DataTable: linhas planas com divisória, realce que desliza sob o ponteiro, faixa
// de situação na lateral, entrada em cascata, densidades, cabeçalho fixo e linha expansível (spec §3).
// Visual aprovado no mockup docs/mockups/listas-gsst-2026-11-25.html (25/11/2026).
import { makeStyles } from '@fluentui/react-components';
import { designTokens, tokensUi } from '../../tokens/tokens';

export const useDataTableStyles = makeStyles({
  // overflowY hidden: o wrapper nunca rola na vertical (não tem altura máxima), e sem isto a entrada
  // em cascata (linha começa 6 px abaixo) acendia uma barra de rolagem por um instante.
  wrap: { overflowX: 'auto', overflowY: 'hidden', position: 'relative' },
  // Com cabeçalho fixo a tabela não rola horizontalmente dentro do card: overflow visível é
  // necessário para que "position: sticky" resolva contra o scroll da página (main.content),
  // não contra este wrapper (que nunca rola verticalmente).
  wrapCabecalhoFixo: { overflowX: 'visible', overflowY: 'visible' },
  // Realce da linha sob o ponteiro: uma peça só, que desliza de linha em linha com mola
  // (molas.seguir) em vez de cada linha acender sozinha.
  realce: {
    position: 'absolute', left: 0, right: 0, top: 0, zIndex: 0, pointerEvents: 'none',
    borderRadius: '12px',
    backgroundColor: `color-mix(in srgb, ${designTokens.colorSurface} 94%, ${designTokens.colorPrimary})`,
    boxShadow: `inset 0 0 0 1px ${tokensUi.bordaSuave}`,
  },
  tabela: {
    position: 'relative', zIndex: 1, width: '100%', borderCollapse: 'separate', borderSpacing: 0,
    fontVariantNumeric: 'tabular-nums',
    '& tbody > tr:first-of-type > td': { borderTopColor: 'transparent' },
  },
  compacta: {},
  cabecalhoFixo: { '& thead th': { position: 'sticky', top: 0, backgroundColor: designTokens.colorSurface, zIndex: 2 } },
  th: {
    textAlign: 'left', fontSize: '10.5px', lineHeight: '14px', fontWeight: 700, letterSpacing: '0.07em', textTransform: 'uppercase',
    color: designTokens.colorNeutralMedium, padding: '6px 14px 10px', borderBottom: `1px solid ${designTokens.colorCardBorder}`, whiteSpace: 'nowrap',
  },
  // Entrada em cascata curta: o atraso de cada linha vem inline (limitado a 0,3 s no total).
  // Com "reduzir movimento" no sistema, index.css zera a animação.
  tr: {
    animationName: { from: { opacity: 0, transform: 'translateY(6px)' }, to: { opacity: 1, transform: 'none' } },
    animationDuration: '420ms',
    animationTimingFunction: 'cubic-bezier(0.2, 0.9, 0.25, 1.15)',
    animationFillMode: 'backwards',
  },
  trClicavel: { cursor: 'pointer', ':focus-visible': { outline: `2px solid ${tokensUi.chrome.ativoFundo}`, outlineOffset: '-2px', borderRadius: '12px' } },
  td: {
    position: 'relative', backgroundColor: 'transparent', padding: '12px 14px', color: designTokens.colorNeutralDark,
    borderTop: `1px solid ${tokensUi.bordaSuave}`, verticalAlign: 'middle',
  },
  tdCompacta: { padding: '8px 12px' },
  tdPrimeira: { paddingLeft: '18px' },
  tdUltima: {},
  // Faixa de situação na lateral esquerda da linha (prop tomLinha).
  faixa: { position: 'absolute', left: '4px', top: '12px', bottom: '12px', width: '3px', borderRadius: '3px' },
  direita: { textAlign: 'right' },
  centro: { textAlign: 'center' },
  // Ações agrupadas numa pílula: lê como um bloco só em vez de vários círculos soltos.
  acoes: {
    display: 'inline-flex', gap: '2px', justifyContent: 'flex-end', alignItems: 'center',
    padding: '3px', borderRadius: tokensUi.raio.full, backgroundColor: tokensUi.status.neutro.fundo,
    // Linha sem nenhuma ação (consumidor devolveu só condicionais falsas): sem pílula vazia.
    ':empty': { display: 'none' },
  },
  // Área expansível empilha o que o consumidor passar: sem isto, título e conteúdo colam.
  // alignItems: flex-start (em vez do stretch padrão de uma coluna flex) evita que um filho sem
  // largura própria (ex.: um <Button> passado direto) vire full-width sem o consumidor pedir.
  expandida: { display: 'flex', flexDirection: 'column', alignItems: 'flex-start', gap: tokensUi.espaco.sm, backgroundColor: designTokens.colorNeutralLight, borderRadius: tokensUi.raio.md, padding: `${tokensUi.espaco.md} ${tokensUi.espaco.lg}`, margin: `0 0 ${tokensUi.espaco.sm}` },
  // Afordância de disclosure (spec §4.5: "▸ Função A" / "▾ Função B") na primeira coluna quando a
  // tabela é expansível — sem isto a única pista de que a linha abre é o cursor de ponteiro.
  celulaComSeta: { display: 'flex', alignItems: 'center', gap: tokensUi.espaco.xs },
  seta: { color: designTokens.colorNeutralMedium, flexShrink: 0, transitionProperty: 'transform', transitionDuration: tokensUi.duracao.rapido },
  setaAberta: { transform: 'rotate(90deg)' },
});

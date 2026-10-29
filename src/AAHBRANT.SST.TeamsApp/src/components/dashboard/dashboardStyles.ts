import { makeStyles, tokens } from '@fluentui/react-components';
import { designTokens, tokensUi } from '@ui';

export const useDashboardStyles = makeStyles({
  // Cabeçalho do Início (proposta C, 04/10): título à esquerda e filtros à direita, sem a moldura de
  // card que existia antes — a barra solta no topo liberava a primeira dobra para os indicadores.
  barraFiltrosDashboard: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    flexWrap: 'wrap',
    gap: '12px 16px',
    marginBottom: '14px',
  },
  tituloPagina: {
    margin: 0,
    fontSize: '22px',
    lineHeight: '28px',
    fontWeight: 700,
    letterSpacing: '-0.02em',
    color: designTokens.colorNeutralDark,
  },
  filtrosDireita: {
    display: 'flex',
    alignItems: 'center',
    flexWrap: 'wrap',
    gap: '10px',
    '@media (max-width: 520px)': {
      width: '100%',
      alignItems: 'stretch',
      flexDirection: 'column',
    },
  },
  grupoPeriodos: {
    display: 'inline-flex',
    alignItems: 'center',
    gap: '2px',
    padding: '3px',
    backgroundColor: designTokens.colorSurface,
    border: `1px solid ${designTokens.colorCardBorder}`,
    borderRadius: '999px',
    '@media (max-width: 520px)': {
      display: 'grid',
      borderRadius: '18px',
      gridTemplateColumns: 'repeat(2, minmax(0, 1fr))',
    },
  },
  botaoPeriodo: {
    minWidth: 'auto',
    height: '32px',
    minHeight: '32px',
    fontWeight: 600,
    color: designTokens.colorNeutralMedium,
    '@media (max-width: 520px)': {
      width: '100%',
      paddingLeft: '4px',
      paddingRight: '4px',
    },
  },
  botaoPeriodoSelecionado: {
    minWidth: 'auto',
    height: '32px',
    minHeight: '32px',
    fontWeight: 600,
    '@media (max-width: 520px)': {
      width: '100%',
      paddingLeft: '4px',
      paddingRight: '4px',
    },
    color: '#ffffff',
    backgroundColor: 'var(--sst-botao-marca)',
    '& .fui-Button__content': {
      color: '#ffffff',
      fontWeight: 700,
    },
    ':hover': {
      color: '#ffffff',
      backgroundColor: 'var(--sst-botao-marca)',
      filter: 'brightness(1.15)',
    },
  },
  filtroObra: {
    display: 'flex',
    alignItems: 'center',
    gap: '8px',
    '@media (max-width: 520px)': {
      alignItems: 'stretch',
      flexDirection: 'column',
      gap: '6px',
    },
    '& .fui-Select': {
      minWidth: '180px',
      '@media (max-width: 520px)': { minWidth: 0 },
    },
  },
  // Faixa única de indicadores: as células dividem uma moldura só (linha de 1px entre elas) em vez de
  // sete cards separados. Flex com wrap e flex-grow, e não grid de colunas fixas: assim a última
  // linha estica e nunca sobra célula vazia mostrando a cor da linha divisória.
  faixaKpis: {
    display: 'flex',
    flexWrap: 'wrap',
    gap: '1px',
    minWidth: 0,
    backgroundColor: designTokens.colorCardBorder,
    border: `1px solid ${designTokens.colorCardBorder}`,
    borderRadius: tokensUi.raio.lg,
    overflow: 'hidden',
    '& > *': {
      flex: '1 1 150px',
      minWidth: 0,
      width: 'auto !important',
      border: 'none !important',
      borderRadius: '0 !important',
      boxShadow: 'none !important',
    },
  },
  // Coluna à direita das ocorrências: ASO em cima e Taxa de Gravidade embaixo, esticada até a altura
  // do card de ocorrências para não deixar lacuna (pedido do usuário, 04/10).
  colunaDireita: {
    display: 'flex',
    flexDirection: 'column',
    gap: '16px',
    minWidth: 0,
    // O ASO é clicável (cardAcionavel, height:100%); numa coluna flex isso brigaria com o card que
    // estica, então o primeiro filho fica com a altura do próprio conteúdo.
    '& > div:first-child': { height: 'auto', flexShrink: 0 },
  },
  // Card que cresce para ocupar o espaço que sobra da coluna; o corpo reparte o conteúdo na vertical.
  cardEsticado: {
    flexGrow: 1,
    display: 'flex',
    flexDirection: 'column',
    '& > div:last-child': {
      flexGrow: 1,
      display: 'flex',
      flexDirection: 'column',
      justifyContent: 'space-between',
      gap: '14px',
    },
  },
  // Gráfico de tendência ocupa 2/3 e o donut 1/3 (mosaico compacto).
  gradeDoisParaUm: {
    display: 'grid',
    gridTemplateColumns: 'minmax(0, 2fr) minmax(0, 1fr)',
    gap: '16px',
    marginBottom: '16px',
    minWidth: 0,
    '@media (max-width: 900px)': {
      gridTemplateColumns: '1fr',
    },
  },
  filtros: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
    gap: '12px',
    marginBottom: '20px',
  },
  // Linha que combina a grade de KPIs com o mini calendário (largura fixa de 200px, ver
  // MiniCalendarioCard). Grid em vez de flex: com flex e flex-basis:auto, o tamanho "auto" da
  // grade de KPIs é o max-content do conteúdo, não o espaço realmente disponível — isso forçava o
  // calendário a quebrar de linha mesmo em telas onde os dois cabiam lado a lado (768px, achado ao
  // corrigir o layout mobile em 21/09). Com colunas de grid explícitas, quem decide é o breakpoint.
  linhaKpisCalendario: {
    display: 'grid',
    gridTemplateColumns: '1fr',
    gap: '16px',
    alignItems: 'start',
    marginBottom: '16px',
  },
  // No celular o card ocupava uma linha inteira só pra mostrar um calendário mensal, que já existe
  // por completo em /calendario — pedido do usuário (21/09) pra ganhar espaço de tela lá.
  calendarioEscondidoNoMobile: {
    '@media (max-width: 620px)': { display: 'none' },
  },
  // Grade dos 7 indicadores do topo (6 KPIs + Taxa de Gravidade). Colunas FIXAS por largura, e não
  // auto-fit: com auto-fit o número de colunas variava com a tela e o 7º cartão caía sozinho numa
  // segunda linha ao lado de um vazio (13/09). Com colunas fixas, as linhas ficam sempre alinhadas
  // (7 em uma linha em telas largas; 4+3 em médias; 2 por linha em estreitas).
  gradeKpis: {
    display: 'grid',
    gridTemplateColumns: '1fr',
    gap: '16px',
    flexGrow: 1,
    minWidth: 0,
    // Faixas mutuamente exclusivas de propósito: o Griffel não garante a ordem das @media na folha
    // gerada, e com min-width em cascata a regra de 4 colunas vencia a de 7 em tela larga.
    '@media (min-width: 480px) and (max-width: 959px)': { gridTemplateColumns: 'repeat(2, minmax(0, 1fr))' },
    '@media (min-width: 960px) and (max-width: 1559px)': { gridTemplateColumns: 'repeat(4, minmax(0, 1fr))' },
    '@media (min-width: 1560px)': { gridTemplateColumns: 'repeat(7, minmax(0, 1fr))' },
  },
  dashboardGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(min(100%, 340px), 1fr))',
    gap: '16px',
    marginBottom: '16px',
    minWidth: 0,
    '@media (max-width: 680px)': {
      gridTemplateColumns: '1fr',
    },
  },
  // Última linha do Início: Aptidão por treinamento, Conformidade por obra, Vencimentos e Atividade
  // em duas colunas iguais (04/10).
  gradeDoisColunas: {
    display: 'grid',
    gridTemplateColumns: 'repeat(2, minmax(0, 1fr))',
    gap: '16px',
    marginBottom: '16px',
    minWidth: 0,
    '@media (max-width: 900px)': {
      gridTemplateColumns: '1fr',
    },
  },
  // Grade única do Início (proposta A, 28/10/2026): todos os blocos na mesma régua de 12 colunas,
  // cards da mesma linha com a mesma altura. Faixas de @media mutuamente exclusivas pelo mesmo motivo
  // da gradeKpis (o Griffel não garante a ordem das @media na folha gerada).
  gradeInicio: {
    display: 'grid',
    gridTemplateColumns: 'repeat(12, minmax(0, 1fr))',
    gap: '16px',
    marginBottom: '16px',
    minWidth: 0,
  },
  // Célula da grade: o card de dentro ocupa a célula inteira, nas duas direções.
  celula: {
    display: 'flex',
    alignItems: 'stretch',
    minWidth: 0,
    '& > *': { flexGrow: 1, flexShrink: 1, flexBasis: 'auto', minWidth: 0, width: '100%' },
  },
  // Indicadores menores: 4 lado a lado no largo, 2×2 do médio para baixo.
  coluna2: {
    gridColumn: 'span 2',
    '@media (max-width: 1100px)': { gridColumn: 'span 6' },
  },
  coluna4: {
    gridColumn: 'span 4',
    '@media (min-width: 621px) and (max-width: 1100px)': { gridColumn: 'span 6' },
    '@media (max-width: 620px)': { gridColumn: 'span 12' },
  },
  // Bloco que, do médio para baixo, ocupa a linha toda (destaque da faixa, ASO e Taxa de gravidade),
  // para nenhum card sobrar sozinho numa linha.
  coluna4Larga: {
    gridColumn: 'span 4',
    '@media (max-width: 1100px)': { gridColumn: 'span 12' },
  },
  coluna6: {
    gridColumn: 'span 6',
    '@media (max-width: 900px)': { gridColumn: 'span 12' },
  },
  coluna8: {
    gridColumn: 'span 8',
    '@media (max-width: 1100px)': { gridColumn: 'span 12' },
  },
  // "Atualizado há X min" + botão de atualizar, ao lado dos filtros.
  atualizado: {
    display: 'flex',
    alignItems: 'center',
    gap: '4px',
    fontSize: '12px',
    fontWeight: 600,
    color: designTokens.colorNeutralMedium,
    whiteSpace: 'nowrap',
  },
  // Barra de proporção dentro do card de destaque ("Aptos para o campo").
  barraDestaque: { height: '6px', borderRadius: '3px', backgroundColor: 'rgba(255,255,255,0.22)', overflow: 'hidden', margin: '2px 0 4px', minWidth: '160px' },
  barraDestaqueValor: { display: 'block', height: '100%', backgroundColor: '#d1fae5', borderRadius: '3px', transitionProperty: 'width', transitionDuration: '1s' },
  totalDestaque: { fontSize: '16px', fontWeight: 600, opacity: 0.8 },
  // Link de rodapé das listas (Ver agenda completa, Ver toda a atividade).
  linkRodape: {
    border: 'none',
    backgroundColor: 'transparent',
    color: 'var(--sst-acao-marca-tinta)',
    fontWeight: 700,
    fontSize: '12.5px',
    cursor: 'pointer',
    padding: '4px 6px',
    borderRadius: '6px',
    ':hover': { backgroundColor: 'var(--sst-acao-marca-wash)' },
  },
  chipPrazo: { fontSize: '11.5px', fontWeight: 700, borderRadius: '10px', padding: '2px 9px', whiteSpace: 'nowrap' },
  kpiIcone: {
    width: '34px',
    height: '34px',
    borderRadius: tokensUi.raio.md,
    backgroundColor: designTokens.colorNeutralLight,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: '2px',
  },
  kpiValor: {
    fontSize: '28px',
    fontWeight: 800,
    letterSpacing: '-0.01em',
    fontVariantNumeric: 'tabular-nums',
    lineHeight: '32px',
  },
  kpiRotulo: {
    color: designTokens.colorNeutralMedium,
    fontSize: '12.5px',
    fontWeight: 600,
  },
  chartRow: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(min(100%, 340px), 1fr))',
    gap: '16px',
    marginBottom: '16px',
  },
  chartCard: {
    backgroundColor: designTokens.colorSurface,
    borderRadius: '12px',
    border: `1px solid ${designTokens.colorCardBorder}`,
    boxShadow: designTokens.cardShadow,
    padding: '18px 20px',
  },
  chartTitulo: {
    fontSize: '14px',
    fontWeight: 700,
    marginBottom: '4px',
  },
  chartSubtitulo: {
    fontSize: '12px',
    color: tokens.colorNeutralForeground3,
    marginBottom: '12px',
  },
  motorPainel: {
    backgroundColor: designTokens.colorSurface,
    borderRadius: '12px',
    border: `1px solid ${designTokens.colorCardBorder}`,
    boxShadow: designTokens.cardShadow,
    padding: '18px 20px',
  },
  motorCabecalho: {
    display: 'flex',
    flexWrap: 'wrap',
    justifyContent: 'space-between',
    alignItems: 'center',
    gap: '12px',
    marginBottom: '16px',
  },
  motorLista: {
    display: 'flex',
    flexDirection: 'column',
    gap: '8px',
    maxHeight: '420px',
    overflowY: 'auto',
  },
  motorLinha: {
    display: 'grid',
    gridTemplateColumns: '1fr auto',
    alignItems: 'center',
    gap: '12px',
    padding: '10px 14px',
    borderRadius: '6px',
    backgroundColor: tokens.colorNeutralBackground2,
  },
  motorLinhaBloqueada: {
    borderLeft: `3px solid ${tokens.colorPaletteRedForeground1}`,
  },
  motorLinhaApta: {
    borderLeft: `3px solid ${tokens.colorPaletteGreenForeground1}`,
  },
  feed: {
    display: 'flex',
    flexDirection: 'column',
  },
  feedItem: {
    display: 'flex',
    alignItems: 'flex-start',
    gap: '12px',
    padding: '12px 4px',
    borderBottom: `1px solid ${designTokens.colorCardBorder}`,
    ':last-child': {
      borderBottom: 'none',
    },
  },
  feedItemAcionavel: {
    cursor: 'pointer',
    ':hover': { backgroundColor: designTokens.colorNeutralLight },
    ':focus-visible': { outline: `2px solid ${designTokens.colorPrimary}`, outlineOffset: '-2px' },
  },
  feedIcone: {
    width: '34px',
    height: '34px',
    borderRadius: tokensUi.raio.md,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    flexShrink: 0,
    backgroundColor: designTokens.colorNeutralLight,
    color: designTokens.colorNeutralMedium,
  },
  feedIconeAlerta: {
    backgroundColor: designTokens.colorAlertWash,
    color: designTokens.colorAlert,
  },
  feedIconeAtencao: {
    backgroundColor: designTokens.colorWarningWash,
    color: designTokens.colorWarning,
  },
  feedIconeBom: {
    backgroundColor: designTokens.colorSuccessWash,
    color: designTokens.colorSuccess,
  },
  feedIconeInfo: {
    backgroundColor: designTokens.colorInfoWash,
    color: designTokens.colorInfo,
  },
  feedCorpo: {
    flex: 1,
    minWidth: 0,
  },
  feedTitulo: {
    fontSize: '14px',
    lineHeight: '20px',
    fontWeight: 700,
    color: designTokens.colorNeutralDark,
  },
  feedMeta: {
    fontSize: '12px',
    lineHeight: '16px',
    color: designTokens.colorNeutralMedium,
    fontWeight: 600,
    marginTop: '1px',
  },
  feedHora: {
    fontSize: '12px',
    lineHeight: '16px',
    color: designTokens.colorNeutralMedium,
    fontWeight: 600,
    flexShrink: 0,
    whiteSpace: 'nowrap',
    paddingTop: '2px',
  },
  // Wrapper clicável dos cards do Dashboard. É flex para o card interno esticar na altura da linha,
  // mas um filho flex NÃO estica na largura por padrão (flex: 0 1 auto) — o card encolhia para a
  // largura do conteúdo e sobrava um buraco na coluna da grade ("dashboard desalinhado", 13/09).
  // O seletor de filho força o card a ocupar a célula inteira nas duas direções.
  cardAcionavel: {
    height: '100%',
    display: 'flex',
    alignItems: 'stretch',
    cursor: 'pointer',
    borderRadius: tokensUi.raio.lg,
    '& > *': {
      flexGrow: 1,
      flexShrink: 1,
      flexBasis: 'auto',
      minWidth: 0,
      width: '100%',
      transitionProperty: 'border-color, box-shadow',
      transitionDuration: '150ms',
    },
    // Mesmo contorno do KpiCard ao passar o mouse, no verde translúcido dos botões (28/10/2026).
    ':hover > *': {
      border: '1px solid var(--sst-botao-marca)',
      boxShadow: 'inset 0 0 0 1px color-mix(in srgb, var(--sst-botao-marca) 30%, transparent), 0 4px 16px color-mix(in srgb, var(--sst-botao-marca) 12%, transparent)',
    },
    ':focus-visible': {
      outline: '2px solid var(--sst-botao-marca)',
      outlineOffset: '2px',
    },
  },
});

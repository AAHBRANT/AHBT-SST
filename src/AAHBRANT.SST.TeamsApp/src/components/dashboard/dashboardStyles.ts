import { makeStyles, tokens } from '@fluentui/react-components';
import { designTokens, tokensUi } from '@ui';

export const useDashboardStyles = makeStyles({
  barraFiltrosDashboard: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    gap: '16px',
    marginBottom: '16px',
    padding: '10px 14px',
    backgroundColor: designTokens.colorSurface,
    border: `1px solid ${designTokens.colorCardBorder}`,
    borderRadius: '14px',
    boxShadow: '0 8px 20px rgba(15, 23, 42, 0.04)',
    '@media (max-width: 900px)': {
      alignItems: 'stretch',
      flexDirection: 'column',
    },
  },
  grupoPeriodos: {
    display: 'flex',
    alignItems: 'center',
    gap: '6px',
    flexWrap: 'wrap',
  },
  botaoPeriodo: {
    minWidth: 'auto',
    height: '32px',
    borderRadius: '8px',
    fontWeight: 700,
    color: designTokens.colorNeutralMedium,
  },
  botaoPeriodoAtivo: {
    color: '#16a34a',
    backgroundColor: 'color-mix(in srgb, #16a34a 10%, transparent)',
    '& .fui-Button__content': {
      color: '#16a34a',
      fontWeight: 800,
    },
    ':hover': {
      color: '#16a34a',
      backgroundColor: 'color-mix(in srgb, #16a34a 14%, transparent)',
    },
  },
  filtroObra: {
    display: 'flex',
    alignItems: 'center',
    gap: '8px',
    maxWidth: '360px',
    width: '100%',
    justifyContent: 'flex-end',
    '@media (max-width: 900px)': {
      justifyContent: 'flex-start',
      maxWidth: 'none',
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
    gridTemplateColumns: '1fr 200px',
    gap: '16px',
    alignItems: 'start',
    marginBottom: '16px',
    '@media (max-width: 620px)': { gridTemplateColumns: '1fr' },
  },
  // Grade dos 7 indicadores do topo (6 KPIs + Taxa de Gravidade). Colunas FIXAS por largura, e não
  // auto-fit: com auto-fit o número de colunas variava com a tela e o 7º cartão caía sozinho numa
  // segunda linha ao lado de um vazio (13/09). Com colunas fixas, as linhas ficam sempre alinhadas
  // (7 em uma linha em telas largas; 4+3 em médias; 2 por linha em estreitas).
  gradeKpis: {
    display: 'grid',
    gridTemplateColumns: 'repeat(2, minmax(0, 1fr))',
    gap: '16px',
    flexGrow: 1,
    minWidth: 0,
    // Faixas mutuamente exclusivas de propósito: o Griffel não garante a ordem das @media na folha
    // gerada, e com min-width em cascata a regra de 4 colunas vencia a de 7 em tela larga.
    '@media (min-width: 960px) and (max-width: 1559px)': { gridTemplateColumns: 'repeat(4, minmax(0, 1fr))' },
    '@media (min-width: 1560px)': { gridTemplateColumns: 'repeat(7, minmax(0, 1fr))' },
  },
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
    gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))',
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
    '& > *': { flexGrow: 1, flexShrink: 1, flexBasis: 'auto', minWidth: 0, width: '100%' },
    ':focus-visible': {
      outline: `2px solid ${designTokens.colorPrimary}`,
      outlineOffset: '2px',
    },
  },
});

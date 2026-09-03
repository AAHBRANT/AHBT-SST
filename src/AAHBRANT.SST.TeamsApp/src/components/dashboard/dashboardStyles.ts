import { makeStyles, tokens } from '@fluentui/react-components';
import { designTokens } from '../../theme';

export const useDashboardStyles = makeStyles({
  filtros: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
    gap: '12px',
    marginBottom: '20px',
  },
  kpiIcone: {
    width: '34px',
    height: '34px',
    borderRadius: '9px',
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
    borderRadius: '9px',
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
    fontSize: '13px',
    fontWeight: 700,
    color: designTokens.colorNeutralDark,
  },
  feedMeta: {
    fontSize: '11.5px',
    color: designTokens.colorNeutralMedium,
    fontWeight: 500,
    marginTop: '1px',
  },
  feedHora: {
    fontSize: '11px',
    color: designTokens.colorNeutralMedium,
    fontWeight: 600,
    flexShrink: 0,
    whiteSpace: 'nowrap',
    paddingTop: '2px',
  },
});

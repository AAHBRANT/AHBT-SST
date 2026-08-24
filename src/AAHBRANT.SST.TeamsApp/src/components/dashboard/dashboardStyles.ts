import { makeStyles, tokens } from '@fluentui/react-components';

export const useDashboardStyles = makeStyles({
  filtros: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
    gap: '12px',
    marginBottom: '20px',
  },
  kpiValor: {
    fontSize: '28px',
    fontWeight: 700,
    lineHeight: '32px',
  },
  kpiRotulo: {
    color: tokens.colorNeutralForeground3,
    fontSize: '13px',
  },
  chartRow: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))',
    gap: '16px',
    marginBottom: '16px',
  },
  chartCard: {
    backgroundColor: '#FFFFFF',
    borderRadius: '8px',
    boxShadow: '0 1px 3px rgba(0, 0, 0, 0.05)',
    padding: '20px',
  },
  chartTitulo: {
    fontSize: '15px',
    fontWeight: 600,
    marginBottom: '4px',
  },
  chartSubtitulo: {
    fontSize: '12px',
    color: tokens.colorNeutralForeground3,
    marginBottom: '12px',
  },
  motorPainel: {
    backgroundColor: '#FFFFFF',
    borderRadius: '8px',
    boxShadow: '0 1px 3px rgba(0, 0, 0, 0.05)',
    padding: '20px',
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
});

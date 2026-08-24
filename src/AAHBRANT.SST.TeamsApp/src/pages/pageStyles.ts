import { makeStyles, tokens } from '@fluentui/react-components';

export const usePageStyles = makeStyles({
  card: {
    backgroundColor: '#FFFFFF',
    borderRadius: '8px',
    boxShadow: '0 1px 3px rgba(0, 0, 0, 0.05)',
    padding: '20px',
  },
  toolbar: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: '16px',
  },
  form: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
    gap: '12px',
    marginBottom: '20px',
  },
  formActions: {
    display: 'flex',
    justifyContent: 'flex-end',
    gap: '8px',
    marginTop: '4px',
  },
  erro: {
    color: tokens.colorPaletteRedForeground1,
    marginBottom: '12px',
  },
});

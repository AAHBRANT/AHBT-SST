import { makeStyles } from '@fluentui/react-components';
import { designTokens } from '@ui';

export const useAtividadesPorFuncaoStyles = makeStyles({
  linha: {
    display: 'grid', gridTemplateColumns: '48px minmax(0, 1fr) auto', columnGap: '10px', alignItems: 'center',
    padding: '6px 14px 6px 6px', minHeight: '72px',
    borderBottom: `1px solid ${designTokens.colorCardBorder}`,
    '@media (max-width: 640px)': { gridTemplateColumns: '48px minmax(0, 1fr)', rowGap: '6px' },
  },
  situacao: {
    display: 'flex', gap: '8px', alignItems: 'center', flexWrap: 'wrap', justifyContent: 'flex-end',
    '@media (max-width: 640px)': { gridColumn: '2', justifyContent: 'flex-start', paddingBottom: '6px' },
  },
});

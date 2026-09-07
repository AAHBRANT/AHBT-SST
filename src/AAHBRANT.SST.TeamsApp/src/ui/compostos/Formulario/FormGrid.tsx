import type { ReactNode } from 'react';
import { makeStyles, mergeClasses } from '@fluentui/react-components';
import { tokensUi } from '../../tokens/tokens';

const useStyles = makeStyles({
  grid: { display: 'grid', gridTemplateColumns: 'repeat(12, 1fr)', gap: tokensUi.espaco.lg, marginBottom: '20px',
    // Único breakpoint do app hoje (pageStyles.formGrid) — fica aqui dentro.
    '@media (max-width: 900px)': { gridTemplateColumns: 'repeat(1, 1fr)' } },
  s1: { gridColumn: 'span 1' }, s2: { gridColumn: 'span 2' }, s3: { gridColumn: 'span 3' }, s4: { gridColumn: 'span 4' }, s5: { gridColumn: 'span 5' }, s6: { gridColumn: 'span 6' }, s8: { gridColumn: 'span 8' }, s12: { gridColumn: 'span 12' },
});

// Grade de 12 colunas para formulários com larguras deliberadas (spec §3). Formaliza
// usePageStyles.formGrid + col2..col12. Abaixo de 900px tudo vira uma coluna.
export function FormGrid({ children }: { children: ReactNode }) {
  const e = useStyles();
  return <div className={e.grid}>{children}</div>;
}

export function Campo({ span = 12, children }: { span?: 1 | 2 | 3 | 4 | 5 | 6 | 8 | 12; children: ReactNode }) {
  const e = useStyles();
  const classe = { 1: e.s1, 2: e.s2, 3: e.s3, 4: e.s4, 5: e.s5, 6: e.s6, 8: e.s8, 12: e.s12 }[span];
  return <div className={mergeClasses(classe)}>{children}</div>;
}

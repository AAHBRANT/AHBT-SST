import type { ReactNode } from 'react';
import { makeStyles, mergeClasses } from '@fluentui/react-components';
import { tokensUi } from '../../tokens/tokens';

const useStyles = makeStyles({
  // minmax(0, 1fr) em vez de 1fr: `1fr` é `minmax(auto, 1fr)`, cujo mínimo é o conteúdo — sem isso
  // uma coluna nunca encolhe abaixo da largura intrínseca do controle que está dentro dela.
  grid: { display: 'grid', gridTemplateColumns: 'repeat(12, minmax(0, 1fr))', gap: tokensUi.espaco.lg, marginBottom: '20px',
    // Único breakpoint do app hoje (pageStyles.formGrid) — fica aqui dentro.
    '@media (max-width: 900px)': { gridTemplateColumns: 'repeat(1, minmax(0, 1fr))' } },
  // Os controles crus do Fluent (Input/Select/Textarea) trazem min-width próprio: numa faixa mais
  // estreita que essa largura intrínseca — span 2 de 12 dá ~112px e um Input pede ~205px — o
  // controle vazava para fora da célula, por cima do campo vizinho, em vez de encolher. Caso real:
  // a linha "Nova ação" da NaoConformidadeDetalhePage (piloto 2). O Combobox já nasce com
  // minWidth 0 no SeletorPesquisavel; aqui o mesmo passa a valer para os primitivos crus.
  campo: { minWidth: 0,
    '& .fui-Input, & .fui-Select, & .fui-Textarea, & .fui-Combobox, & .fui-Dropdown': { minWidth: 0, width: '100%' } },
  // Sem isso, `gridColumn: span N` força a criação de colunas implícitas mesmo com uma única
  // coluna explícita (repeat(1, 1fr) acima), e os campos continuariam lado a lado abaixo de
  // 900px. Cada classe sN colapsa para span 1 dentro do mesmo breakpoint para garantir o
  // empilhamento.
  s1: { gridColumn: 'span 1', '@media (max-width: 900px)': { gridColumn: 'span 1' } },
  s2: { gridColumn: 'span 2', '@media (max-width: 900px)': { gridColumn: 'span 1' } },
  s3: { gridColumn: 'span 3', '@media (max-width: 900px)': { gridColumn: 'span 1' } },
  s4: { gridColumn: 'span 4', '@media (max-width: 900px)': { gridColumn: 'span 1' } },
  s5: { gridColumn: 'span 5', '@media (max-width: 900px)': { gridColumn: 'span 1' } },
  s6: { gridColumn: 'span 6', '@media (max-width: 900px)': { gridColumn: 'span 1' } },
  s8: { gridColumn: 'span 8', '@media (max-width: 900px)': { gridColumn: 'span 1' } },
  s12: { gridColumn: 'span 12', '@media (max-width: 900px)': { gridColumn: 'span 1' } },
});

// Grade de 12 colunas para formulários com larguras deliberadas (spec §3). Formaliza
// usePageStyles.formGrid + col2..col12. Abaixo de 900px, cada Campo ocupa a única coluna
// disponível (empilhamento total, independentemente do span original).
export function FormGrid({ children }: { children: ReactNode }) {
  const e = useStyles();
  return <div className={e.grid}>{children}</div>;
}

export function Campo({ span = 12, children }: { span?: 1 | 2 | 3 | 4 | 5 | 6 | 8 | 12; children: ReactNode }) {
  const e = useStyles();
  const classe = { 1: e.s1, 2: e.s2, 3: e.s3, 4: e.s4, 5: e.s5, 6: e.s6, 8: e.s8, 12: e.s12 }[span];
  return <div className={mergeClasses(e.campo, classe)}>{children}</div>;
}

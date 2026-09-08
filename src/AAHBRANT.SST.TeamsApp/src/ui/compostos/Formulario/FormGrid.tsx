import type { ReactNode } from 'react';
import { makeStyles, mergeClasses } from '@fluentui/react-components';
import { tokensUi } from '../../tokens/tokens';

const useStyles = makeStyles({
  // minmax(0, 1fr) e não 1fr: com `1fr` o mínimo da faixa é `auto` (min-content), então rótulo
  // longo ou largura mínima de <Input> empurram as 12 faixas para além do container e o formulário
  // ganha barra de rolagem horizontal. Apareceu no piloto 1, dentro do PainelLateral de 560px
  // (spec §5.1: o componente é que se ajusta, não a página).
  grid: { display: 'grid', gridTemplateColumns: 'repeat(12, minmax(0, 1fr))', gap: tokensUi.espaco.lg, marginBottom: '20px',
    // Único breakpoint do app hoje (pageStyles.formGrid) — fica aqui dentro.
    '@media (max-width: 900px)': { gridTemplateColumns: 'repeat(1, minmax(0, 1fr))' },
    // O <input> nativo tem largura intrínseca de ~200px (o `size=20` do HTML) e ela sobe pela
    // cadeia: como Field é grid e a raiz do controle do Fluent é item de grid com `min-width: auto`
    // (= min-content), em faixa mais estreita que isso — Campo span={4} dentro do PainelLateral de
    // 560px — o campo vazava para fora da grade. Precisa de minWidth 0 em toda a cadeia (Field,
    // raiz do controle e o elemento nativo), não só no elemento nativo.
    '& .fui-Field': { minWidth: 0 },
    '& .fui-Input, & .fui-Select, & .fui-Combobox, & .fui-Textarea, & .fui-Dropdown': { minWidth: 0 },
    '& input, & select, & textarea': { minWidth: 0 } },
  // Sem isso, `gridColumn: span N` força a criação de colunas implícitas mesmo com uma única
  // coluna explícita (repeat(1, …) acima), e os campos continuariam lado a lado abaixo de
  // 900px. Cada classe sN colapsa para span 1 dentro do mesmo breakpoint para garantir o
  // empilhamento. `minWidth: 0` deixa o campo encolher junto com a faixa.
  s1: { gridColumn: 'span 1', minWidth: 0, '@media (max-width: 900px)': { gridColumn: 'span 1' } },
  s2: { gridColumn: 'span 2', minWidth: 0, '@media (max-width: 900px)': { gridColumn: 'span 1' } },
  s3: { gridColumn: 'span 3', minWidth: 0, '@media (max-width: 900px)': { gridColumn: 'span 1' } },
  s4: { gridColumn: 'span 4', minWidth: 0, '@media (max-width: 900px)': { gridColumn: 'span 1' } },
  s5: { gridColumn: 'span 5', minWidth: 0, '@media (max-width: 900px)': { gridColumn: 'span 1' } },
  s6: { gridColumn: 'span 6', minWidth: 0, '@media (max-width: 900px)': { gridColumn: 'span 1' } },
  s8: { gridColumn: 'span 8', minWidth: 0, '@media (max-width: 900px)': { gridColumn: 'span 1' } },
  s12: { gridColumn: 'span 12', minWidth: 0, '@media (max-width: 900px)': { gridColumn: 'span 1' } },
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
  return <div className={mergeClasses(classe)}>{children}</div>;
}

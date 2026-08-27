import { makeStyles, shorthands, tokens } from '@fluentui/react-components';

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

// Abas em formato de pílula (padrão visual aprovado para todas as abas do sistema): sobrescreve as
// classes estáveis do Fluent (fui-Tab / fui-Tab__icon / fui-Tab__content) em vez de substituir o
// componente Tab/TabList, então continua funcionando com toda a lógica de seleção/acessibilidade nativa.
export const usePillTabStyles = makeStyles({
  lista: {
    display: 'flex',
    flexWrap: 'wrap',
    columnGap: '8px',
    rowGap: '8px',
    marginBottom: '16px',
    '& .fui-Tab': {
      backgroundColor: tokens.colorNeutralBackground1,
      ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke2),
      ...shorthands.borderRadius('10px'),
      color: tokens.colorNeutralForeground3,
      fontWeight: 600,
      fontSize: '13px',
      ...shorthands.padding('10px', '18px'),
      minHeight: 'auto',
      whiteSpace: 'nowrap',
    },
    '& .fui-Tab::before': {
      display: 'none',
    },
    '& .fui-Tab::after': {
      display: 'none',
    },
    // O Fluent aplica cor própria no conteúdo/ícone internos do Tab (não herdam do botão pai) —
    // forçar herança aqui garante que a cor do texto acompanhe o estado (hover/selecionado) do botão.
    '& .fui-Tab__content': {
      color: 'inherit',
    },
    '& .fui-Tab__icon': {
      color: 'inherit',
    },
    '& .fui-Tab:hover': {
      backgroundColor: tokens.colorNeutralBackground1Hover,
      color: tokens.colorBrandForeground1,
    },
    '& .fui-Tab[aria-selected="true"]': {
      backgroundColor: tokens.colorBrandBackground,
      ...shorthands.borderColor(tokens.colorBrandBackground),
      color: tokens.colorNeutralForegroundOnBrand,
      boxShadow: '0 4px 12px rgba(123, 30, 43, 0.25)',
    },
    '& .fui-Tab[aria-selected="true"]:hover': {
      backgroundColor: tokens.colorBrandBackgroundHover,
      color: tokens.colorNeutralForegroundOnBrand,
    },
  },
});

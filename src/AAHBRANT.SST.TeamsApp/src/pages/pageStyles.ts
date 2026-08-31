import { makeStyles, shorthands, tokens } from '@fluentui/react-components';
import { designTokens } from '../theme';

export const usePageStyles = makeStyles({
  card: {
    backgroundColor: '#FFFFFF',
    borderRadius: '12px',
    border: `1px solid ${designTokens.colorCardBorder}`,
    boxShadow: designTokens.cardShadow,
    padding: '18px 20px',
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

// KPI (cartão de indicador — Hub Gênesis SST): ícone em caixa, valor grande, rótulo e uma pílula
// de variação opcional (neutra/positiva/atenção).
export const useKpiStyles = makeStyles({
  linha: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(185px, 1fr))',
    gap: '16px',
    marginBottom: '16px',
  },
  cartao: {
    display: 'flex',
    flexDirection: 'column',
    gap: '10px',
  },
  icone: {
    width: '34px',
    height: '34px',
    borderRadius: '9px',
    backgroundColor: designTokens.colorNeutralLight,
    color: designTokens.colorPrimary,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
  },
  valor: {
    fontSize: '28px',
    fontWeight: 800,
    letterSpacing: '-0.01em',
    fontVariantNumeric: 'tabular-nums',
  },
  rotulo: {
    fontSize: '12.5px',
    color: designTokens.colorNeutralMedium,
    fontWeight: 600,
  },
  variacao: {
    display: 'inline-flex',
    alignItems: 'center',
    gap: '5px',
    fontSize: '11.5px',
    fontWeight: 700,
    width: 'fit-content',
    padding: '3px 8px',
    borderRadius: '20px',
  },
  variacaoNeutra: {
    color: designTokens.colorNeutralMedium,
    backgroundColor: designTokens.colorNeutralLight,
  },
  variacaoBoa: {
    color: designTokens.colorSuccess,
    backgroundColor: designTokens.colorSuccessWash,
  },
  variacaoAtencao: {
    color: '#9A6B04',
    backgroundColor: designTokens.colorWarningWash,
  },
  variacaoAlerta: {
    color: designTokens.colorAlert,
    backgroundColor: designTokens.colorAlertWash,
  },
});

// Chip de status (Hub Gênesis SST): mesma pílula usada em ASO, EPI, treinamentos, PT/APR etc.
export const useStatusChipStyles = makeStyles({
  chip: {
    display: 'inline-flex',
    alignItems: 'center',
    gap: '5px',
    fontSize: '10.5px',
    fontWeight: 700,
    padding: '3px 8px',
    borderRadius: '20px',
    whiteSpace: 'nowrap',
  },
  ok: {
    color: designTokens.colorSuccess,
    backgroundColor: designTokens.colorSuccessWash,
  },
  pendente: {
    color: '#9A6B04',
    backgroundColor: designTokens.colorWarningWash,
  },
  vencido: {
    color: designTokens.colorAlert,
    backgroundColor: designTokens.colorAlertWash,
  },
  neutro: {
    color: designTokens.colorNeutralMedium,
    backgroundColor: designTokens.colorNeutralLight,
  },
  info: {
    color: designTokens.colorInfo,
    backgroundColor: designTokens.colorInfoWash,
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

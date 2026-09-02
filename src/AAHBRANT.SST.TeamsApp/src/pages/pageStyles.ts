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

// KPI (cartão de indicador — Dashboard principal): valor grande + rótulo à esquerda, ícone em
// círculo à direita, com pílulas de variação opcionais (neutra/positiva/atenção/alerta).
export const useKpiStyles = makeStyles({
  linha: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(185px, 1fr))',
    gap: '16px',
    marginBottom: '16px',
  },
  cartao: {
    display: 'flex',
    flexDirection: 'row',
    alignItems: 'flex-start',
    justifyContent: 'space-between',
    gap: '10px',
  },
  textos: {
    display: 'flex',
    flexDirection: 'column',
    gap: '6px',
    minWidth: 0,
  },
  icone: {
    width: '42px',
    height: '42px',
    borderRadius: '50%',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    flexShrink: 0,
  },
  iconeInfo: {
    backgroundColor: designTokens.colorInfoWash,
    color: designTokens.colorInfo,
  },
  iconeSucesso: {
    backgroundColor: designTokens.colorSuccessWash,
    color: designTokens.colorSuccess,
  },
  iconeAtencao: {
    backgroundColor: designTokens.colorWarningWash,
    color: '#9A6B04',
  },
  iconeAlerta: {
    backgroundColor: designTokens.colorAlertWash,
    color: designTokens.colorAlert,
  },
  valor: {
    fontSize: '26px',
    fontWeight: 800,
    letterSpacing: '-0.01em',
    fontVariantNumeric: 'tabular-nums',
  },
  rotulo: {
    fontSize: '12.5px',
    color: designTokens.colorNeutralMedium,
    fontWeight: 600,
  },
  deltasGrupo: {
    display: 'flex',
    flexWrap: 'wrap',
    gap: '6px',
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

// Checkbox em formato de chip (pedido do usuário, 02/09): listas de seleção múltipla (Responsáveis,
// Equipe, matriz de EPI/treinamento por função, tipos de trabalho especial etc.) só tinham a
// caixinha em si como alvo de clique óbvio — o nome do lado não parecia clicável, mesmo já
// funcionando (o <label> do Fluent já ativa o Checkbox). Envolve cada opção num fundo destacado
// pra deixar claro que a opção inteira é clicável, não só o quadradinho.
export const useCheckboxChipStyles = makeStyles({
  chip: {
    display: 'inline-flex',
    alignItems: 'center',
    backgroundColor: designTokens.colorNeutralLight,
    ...shorthands.borderRadius('999px'),
    ...shorthands.border('1px', 'solid', designTokens.colorCardBorder),
    ...shorthands.padding('6px', '14px', '6px', '10px'),
    cursor: 'pointer',
    transitionProperty: 'background-color, border-color',
    transitionDuration: '120ms',
    ':hover': {
      backgroundColor: tokens.colorNeutralBackground1Hover,
      ...shorthands.borderColor(designTokens.colorPrimary),
    },
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

// Abas em formato quadrado grudado no painel abaixo (padrão visual aprovado para todas as abas do
// sistema, 02/09) — substitui o formato de pílula flutuante anterior. Sobrescreve as classes
// estáveis do Fluent (fui-Tab / fui-Tab__icon / fui-Tab__content) em vez de substituir o componente
// Tab/TabList, então continua funcionando com toda a lógica de seleção/acessibilidade nativa.
export const usePillTabStyles = makeStyles({
  lista: {
    display: 'flex',
    flexWrap: 'wrap',
    columnGap: '2px',
    rowGap: '0',
    marginBottom: '0',
    ...shorthands.borderBottom('1px', 'solid', designTokens.colorCardBorder),
    paddingLeft: '2px',
    '& .fui-Tab': {
      backgroundColor: designTokens.colorNeutralLight,
      ...shorthands.border('1px', 'solid', designTokens.colorCardBorder),
      borderBottom: 'none',
      borderRadius: '8px 8px 0 0',
      color: designTokens.colorNeutralMedium,
      fontWeight: 600,
      fontSize: '13px',
      ...shorthands.padding('10px', '20px'),
      minHeight: 'auto',
      whiteSpace: 'nowrap',
      position: 'relative',
      top: '1px',
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
      backgroundColor: designTokens.colorWhite,
      color: designTokens.colorPrimary,
    },
    // A aba ativa usa o mesmo branco do painel abaixo (se funde visualmente com ele) em vez de um
    // preenchimento sólido — só o traço superior na cor da marca indica a seleção.
    '& .fui-Tab[aria-selected="true"]': {
      backgroundColor: designTokens.colorWhite,
      ...shorthands.borderColor(designTokens.colorCardBorder),
      borderTop: `2px solid ${designTokens.colorPrimary}`,
      paddingTop: '9px',
      color: designTokens.colorPrimary,
      fontWeight: 700,
    },
    '& .fui-Tab[aria-selected="true"]:hover': {
      backgroundColor: designTokens.colorWhite,
      color: designTokens.colorPrimary,
    },
  },
});

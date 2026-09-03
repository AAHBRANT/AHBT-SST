import { makeStyles, shorthands, tokens } from '@fluentui/react-components';
import { designTokens } from '../theme';

export const usePageStyles = makeStyles({
  card: {
    backgroundColor: designTokens.colorSurface,
    borderRadius: '16px',
    border: `1px solid ${designTokens.colorCardBorder}`,
    boxShadow: designTokens.cardShadow,
    padding: '24px 28px',
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
  // Rótulo de seção (pedido do usuário, 03/09, réplica de mockup): divide um formulário longo em
  // blocos nomeados ("1. Dados gerais do documento", "2. Abrangência, riscos e exames" etc.) — mesmo
  // texto pequeno em versalete usado por toda referência de mockup deste app (ver usePillTabStyles
  // acima para o mesmo princípio aplicado a abas). Primeiro da lista não herda a margem superior.
  sectionTitle: {
    fontSize: '12px',
    fontWeight: 700,
    textTransform: 'uppercase',
    letterSpacing: '0.05em',
    color: designTokens.colorNeutralMedium,
    marginTop: '24px',
    marginBottom: '14px',
    paddingBottom: '8px',
    borderBottom: `1px solid ${designTokens.colorNeutralLight}`,
  },
  sectionTitleFirst: {
    marginTop: 0,
  },
  // Grade de 12 colunas (pedido do usuário, 03/09, réplica de mockup): para formulários onde cada
  // campo tem uma largura deliberada (ex.: Nome ocupa mais espaço que Versão) em vez do
  // preenchimento automático de `form` acima — usar junto com col2/col3/col4/col6/col12. Empilha em
  // coluna única abaixo de 900px, mesmo breakpoint do mockup de referência.
  formGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(12, 1fr)',
    gap: '16px',
    marginBottom: '20px',
    '@media (max-width: 900px)': {
      gridTemplateColumns: 'repeat(1, 1fr)',
    },
  },
  col12: { gridColumn: 'span 12' },
  col6: { gridColumn: 'span 6' },
  col5: { gridColumn: 'span 5' },
  col4: { gridColumn: 'span 4' },
  col3: { gridColumn: 'span 3' },
  col2: { gridColumn: 'span 2' },
  // Rodapé de formulário longo (pedido do usuário, 03/09, réplica de mockup): texto de ajuda à
  // esquerda + botão de ação à direita, separados do formulário por uma linha. Usar no lugar de
  // formActions quando o formulário já tem um texto informativo (ex.: explicação do fluxo de status)
  // que hoje fica solto abaixo do botão — aqui os dois ficam na mesma linha.
  footer: {
    marginTop: '24px',
    display: 'flex',
    flexWrap: 'wrap',
    justifyContent: 'space-between',
    alignItems: 'center',
    gap: '16px',
    borderTop: `1px solid ${designTokens.colorNeutralLight}`,
    paddingTop: '16px',
  },
  footerInfo: {
    fontSize: '12px',
    color: designTokens.colorNeutralMedium,
    maxWidth: '700px',
  },
  // Campo de "chips" removíveis (pedido do usuário, 03/09, réplica de mockup): usado por campos que
  // guardam uma lista curta como texto único delimitado (ex.: unidades/obras abrangidas de um PCMSO)
  // — ver components/form/ChipsField.tsx, que consome estas classes.
  chipsContainer: {
    display: 'flex',
    flexWrap: 'wrap',
    alignItems: 'center',
    gap: '6px',
    padding: '8px',
    minHeight: '44px',
    border: `1px solid ${designTokens.colorCardBorder}`,
    borderRadius: '8px',
    backgroundColor: designTokens.colorSurface,
  },
  chip: {
    display: 'inline-flex',
    alignItems: 'center',
    gap: '6px',
    backgroundColor: designTokens.colorNeutralLight,
    color: designTokens.colorNeutralDark,
    fontSize: '12px',
    fontWeight: 500,
    padding: '4px 6px 4px 10px',
    borderRadius: '6px',
    border: `1px solid ${designTokens.colorCardBorder}`,
  },
  chipRemove: {
    display: 'inline-flex',
    cursor: 'pointer',
    color: designTokens.colorNeutralMedium,
    ':hover': {
      color: designTokens.colorAlert,
    },
  },
  chipsInput: {
    flex: '1 1 140px',
    minWidth: '140px',
    border: 'none',
    outline: 'none',
    backgroundColor: 'transparent',
    fontSize: '14px',
    color: designTokens.colorNeutralDark,
    fontFamily: 'inherit',
    padding: '4px',
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
    color: designTokens.colorWarning,
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
    color: designTokens.colorWarning,
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
    color: designTokens.colorWarning,
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
      transitionProperty: 'background-color, color, border-color',
      transitionDuration: '150ms',
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
    // Verde (não a cor escura do painel) — pedido do usuário (02/09): o hover das abas estava
    // usando colorSurface, mais escuro que o fundo padrão da aba no modo escuro, o que lia como
    // "fica preto" em vez de destacar.
    '& .fui-Tab:hover': {
      backgroundColor: designTokens.colorSuccessWash,
      color: designTokens.colorSuccess,
    },
    // A aba ativa usa o mesmo branco do painel abaixo (se funde visualmente com ele) em vez de um
    // preenchimento sólido — só o traço superior na cor da marca indica a seleção.
    '& .fui-Tab[aria-selected="true"]': {
      backgroundColor: designTokens.colorSurface,
      ...shorthands.borderColor(designTokens.colorCardBorder),
      borderTop: `2px solid ${designTokens.colorPrimary}`,
      paddingTop: '9px',
      color: designTokens.colorPrimary,
      fontWeight: 700,
    },
    '& .fui-Tab[aria-selected="true"]:hover': {
      backgroundColor: designTokens.colorSurface,
      color: designTokens.colorPrimary,
    },
  },
});

// Sub-abas (pedido do usuário, 02/09): quando um item que antes tinha link próprio na sidebar vira
// aba de um pilar (ver GestaoSstPage/OperacaoPage/PessoasPillarPage/OcorrenciasPage), as abas que
// esse item já tinha (ex.: PGR/GRO tem PGRs | Matriz de Risco | ...) precisam parecer subordinadas
// à aba do pilar acima, não uma segunda fileira de abas iguais — usePillTabStyles nas duas fileiras
// ficava tudo parecendo uma aba só. Pílula pequena e discreta em vez do quadrado grudado.
export const useSubTabStyles = makeStyles({
  // Faixa própria (fundo + espaçamento acima e abaixo), não só uma segunda linha de abas grudada —
  // pedido do usuário (02/09) pra isolar mais claramente a sub-navegação da aba do pilar acima.
  lista: {
    display: 'flex',
    flexWrap: 'wrap',
    gap: '6px',
    marginTop: '18px',
    marginBottom: '20px',
    ...shorthands.padding('8px', '10px'),
    borderRadius: '10px',
    backgroundColor: designTokens.colorNeutralLight,
    '& .fui-Tab': {
      backgroundColor: 'transparent',
      ...shorthands.border('1px', 'solid', 'transparent'),
      borderRadius: '999px',
      color: designTokens.colorNeutralMedium,
      fontWeight: 600,
      fontSize: '12px',
      ...shorthands.padding('6px', '14px'),
      minHeight: 'auto',
      whiteSpace: 'nowrap',
      transitionProperty: 'background-color, color',
      transitionDuration: '150ms',
    },
    '& .fui-Tab::before': {
      display: 'none',
    },
    '& .fui-Tab::after': {
      display: 'none',
    },
    '& .fui-Tab__content': {
      color: 'inherit',
    },
    '& .fui-Tab__icon': {
      color: 'inherit',
    },
    // Verde (não a cor escura do painel) — mesmo ajuste do usePillTabStyles acima: colorSurface
    // era mais escuro que o fundo da faixa de sub-abas no modo escuro, lia como "fica preto".
    '& .fui-Tab:hover': {
      backgroundColor: designTokens.colorSuccessWash,
      color: designTokens.colorSuccess,
    },
    // Verde (pedido do usuário, 03/09) — não vinho: a sub-aba selecionada não segue a mesma cor de
    // marca da aba do pilar acima, usa o verde já adotado no resto do app (rail, hover das abas).
    '& .fui-Tab[aria-selected="true"]': {
      backgroundColor: designTokens.colorSuccess,
      color: designTokens.colorWhite,
      fontWeight: 700,
    },
    '& .fui-Tab[aria-selected="true"]:hover': {
      backgroundColor: designTokens.colorSuccess,
      color: designTokens.colorWhite,
    },
  },
});

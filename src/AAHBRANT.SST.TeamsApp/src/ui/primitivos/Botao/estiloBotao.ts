import { makeStyles, mergeClasses, type ButtonState } from '@fluentui/react-components';

// Padrão único de botão — "Proposta B · Premium" escolhida pelo usuário em 21/10/2026 (catálogo de
// botões): pílula, rótulo em Montserrat, primário vinho com sombra suave, alturas 34/42/50 px.
// Aplicado a TODO botão do Fluent (Button, ToggleButton, MenuButton, CompoundButton, ToolbarButton)
// via customStyleHooks_unstable do FluentProvider (App.tsx) — nenhuma página precisa mudar para
// herdar o padrão, e botão novo já nasce certo.
//
// Como não briga com o className das páginas: quase tudo aqui só REDEFINE variáveis CSS do tema
// (--borderRadiusMedium, --colorBrandBackground…) no próprio botão. Os estilos base do Fluent leem
// essas variáveis, e um className da página que define a propriedade de verdade (backgroundColor,
// borderRadius…) continua vencendo. As poucas propriedades reais ficam em dois grupos:
//   antes  → minHeight e sombra: valem só se nem o Fluent nem a página definirem (página vence);
//   depois → largura do botão só de ícone: precisa vencer o min/max-width fixo do Fluent.
// Botão com visual próprio de verdade (overlay sobre foto, cabeçalho de acordeão) sai do padrão com
// data-botao="livre".
//
// Mapa de appearance (Fluent) → papel no catálogo:
//   primary     → Primário (vinho sólido)
//   secondary   → Neutro (padrão do Fluent quando não há appearance: Cancelar, Fechar)
//   outline     → Secundário (contorno vinho: Voltar, Visualizar, Exportar)
//   subtle      → Terciário (texto vinho legível, fundo ao passar o mouse; só ícone ganha fundo leve)
//   transparent → Terciário sem fundo em repouso

const MARCA_TINTA = 'var(--sst-acao-marca-tinta)';
const MARCA_WASH = 'var(--sst-acao-marca-wash)';
const MARCA_WASH_FORTE = 'var(--sst-botao-marca-wash-forte)';

const useAntes = makeStyles({
  small: { minHeight: '34px' },
  medium: { minHeight: '42px' },
  large: { minHeight: '50px' },
  sombraPrimario: { boxShadow: 'var(--sst-botao-sombra)' },
});

const useDepois = makeStyles({
  base: {
    '--borderRadiusSmall': '999px',
    '--borderRadiusMedium': '999px',
    '--borderRadiusLarge': '999px',
    '--fontFamilyBase': "'Montserrat', 'Segoe UI', -apple-system, BlinkMacSystemFont, sans-serif",
    letterSpacing: '0.005em',
    transitionProperty: 'background, border, color, box-shadow',
  },
  // O Fluent deixa o rótulo pequeno em peso regular (400): no padrão novo todo rótulo é 600.
  small: { '--spacingHorizontalS': '14px', '--fontSizeBase200': '13px', '--lineHeightBase200': '16px', '--fontWeightRegular': 'var(--fontWeightSemibold)' },
  medium: { '--spacingHorizontalM': '20px' },
  large: { '--spacingHorizontalL': '26px' },
  soIconeSmall: { minWidth: '34px', maxWidth: '34px' },
  soIconeMedium: { minWidth: '42px', maxWidth: '42px' },
  soIconeLarge: { minWidth: '50px', maxWidth: '50px' },

  primary: {
    '--colorBrandBackground': 'var(--sst-botao-marca)',
    '--colorBrandBackgroundHover': 'var(--sst-botao-marca-hover)',
    '--colorBrandBackgroundPressed': 'var(--sst-botao-marca-pressionado)',
  },
  secondary: {
    '--colorNeutralStroke1': 'var(--sst-botao-neutro-borda)',
    '--colorNeutralStroke1Hover': 'var(--sst-botao-neutro-borda-hover)',
    '--colorNeutralStroke1Pressed': 'var(--sst-botao-neutro-borda-hover)',
  },
  outline: {
    '--colorNeutralStroke1': MARCA_TINTA,
    '--colorNeutralStroke1Hover': MARCA_TINTA,
    '--colorNeutralStroke1Pressed': MARCA_TINTA,
    '--colorNeutralForeground1': MARCA_TINTA,
    '--colorNeutralForeground1Hover': MARCA_TINTA,
    '--colorNeutralForeground1Pressed': MARCA_TINTA,
    '--colorTransparentBackgroundHover': MARCA_WASH,
    '--colorTransparentBackgroundPressed': MARCA_WASH_FORTE,
  },
  terciario: {
    '--colorNeutralForeground2': MARCA_TINTA,
    '--colorNeutralForeground2Hover': MARCA_TINTA,
    '--colorNeutralForeground2Pressed': MARCA_TINTA,
    '--colorNeutralForeground2BrandHover': MARCA_TINTA,
    '--colorNeutralForeground2BrandPressed': MARCA_TINTA,
    '--colorSubtleBackgroundHover': MARCA_WASH,
    '--colorSubtleBackgroundPressed': MARCA_WASH_FORTE,
    '--colorTransparentBackgroundHover': MARCA_WASH,
    '--colorTransparentBackgroundPressed': MARCA_WASH_FORTE,
  },
  // Regra do catálogo: botão só de ícone nunca fica invisível em repouso.
  subtleSoIcone: { '--colorSubtleBackground': 'var(--sst-botao-neutro-wash)' },
});

type EstadoBotao = Pick<ButtonState, 'appearance' | 'size' | 'iconOnly' | 'disabled' | 'disabledFocusable'> & {
  root: ButtonState['root'] & { 'data-botao'?: string };
};

export function useEstiloBotaoAahbrant(estadoBruto: unknown) {
  const estado = estadoBruto as EstadoBotao;
  const antes = useAntes();
  const depois = useDepois();
  if (!estado?.root || estado.root['data-botao'] === 'livre') return;

  const { appearance = 'secondary', size = 'medium', iconOnly } = estado;
  const desabilitado = estado.disabled || estado.disabledFocusable;
  const tamanho = size === 'small' ? 'Small' : size === 'large' ? 'Large' : 'Medium';

  estado.root.className = mergeClasses(
    antes[size],
    appearance === 'primary' && !desabilitado && antes.sombraPrimario,
    estado.root.className,
    depois.base,
    depois[size],
    iconOnly && depois[`soIcone${tamanho}`],
    appearance === 'primary' && depois.primary,
    appearance === 'secondary' && depois.secondary,
    appearance === 'outline' && depois.outline,
    (appearance === 'subtle' || appearance === 'transparent') && depois.terciario,
    appearance === 'subtle' && iconOnly && depois.subtleSoIcone,
  );
}

// Passado ao FluentProvider raiz; os providers aninhados herdam (shallowMerge do Fluent).
// SplitButton não entra: os dois botões internos dele já são Button/MenuButton e passam por aqui.
export const ganchosEstiloBotao = {
  useButtonStyles_unstable: useEstiloBotaoAahbrant,
  useToggleButtonStyles_unstable: useEstiloBotaoAahbrant,
  useMenuButtonStyles_unstable: useEstiloBotaoAahbrant,
  useCompoundButtonStyles_unstable: useEstiloBotaoAahbrant,
  useToolbarButtonStyles_unstable: useEstiloBotaoAahbrant,
  useToolbarToggleButtonStyles_unstable: useEstiloBotaoAahbrant,
};

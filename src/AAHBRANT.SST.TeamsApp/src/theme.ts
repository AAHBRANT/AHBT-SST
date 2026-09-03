import { createDarkTheme, createLightTheme, type BrandVariants, type Theme } from '@fluentui/react-components';

// Rampa de marca gerada a partir do vinho oficial AAHBRANT #670000.
const aahbrantBrandRamp: BrandVariants = {
  10: '#040000',
  20: '#1F0000',
  30: '#330000',
  40: '#460000',
  50: '#590000',
  60: '#630000',
  70: '#670000', // color-primary
  80: '#7A0101',
  90: '#8C1414',
  100: '#9E2626',
  110: '#B03838',
  120: '#C24A4A',
  130: '#D25C5C',
  140: '#E06F6F',
  150: '#EC8484',
  160: '#F59A9A',
};

// Tema escuro (padrão do app, pedido do usuário 02/09) e tema claro (usado quando o botão de
// dark/light mode alterna pra claro — ver ThemeModeContext.tsx — e sempre nas páginas públicas de
// QR code, que ficam fora do modo escuro do app interno).
export const aahbrantTheme: Theme = {
  ...createDarkTheme(aahbrantBrandRamp),
  colorNeutralBackground1: '#1E293B',
  colorNeutralBackground2: '#0F172A',
  fontFamilyBase: "'Montserrat', -apple-system, BlinkMacSystemFont, sans-serif",
};

export const aahbrantLightTheme: Theme = {
  ...createLightTheme(aahbrantBrandRamp),
  colorNeutralBackground1: '#FFFFFF',
  colorNeutralBackground2: '#F5F5F7',
  fontFamilyBase: "'Montserrat', -apple-system, BlinkMacSystemFont, sans-serif",
};

// Tokens de cor usados pelos estilos próprios do app (fora dos componentes nativos do Fluent, que já
// respondem à troca de tema sozinhos via aahbrantTheme/aahbrantLightTheme acima). Referenciam
// variáveis CSS (ver index.css) em vez de valores fixos: os estilos deste app usam Griffel
// makeStyles, que gera as classes CSS uma única vez — se os valores aqui fossem string fixa, trocar
// de tema não teria efeito nenhum sem recriar todas as classes. Com var(), o mesmo CSS gerado já
// aponta pra variável, e o botão de dark/light mode só precisa trocar o atributo data-theme na raiz
// (ver ThemeModeContext.tsx) pra tudo atualizar junto, sem re-render.
export const designTokens = {
  colorPrimary: 'var(--sst-color-primary)',
  colorSecondary: 'var(--sst-color-secondary)',
  colorNeutralDark: 'var(--sst-color-neutral-dark)',
  colorNeutralMedium: 'var(--sst-color-neutral-medium)',
  colorNeutralLight: 'var(--sst-color-neutral-light)',
  // Branco "de verdade" — só pra casos que precisam ficar brancos independente do tema (texto sobre
  // um círculo vinho, cards das páginas públicas de QR code). Não usar pra fundo de painel/página —
  // ver colorSurface/colorPageBackground, que mudam com o tema.
  colorWhite: 'var(--sst-color-white)',
  colorSuccess: 'var(--sst-color-success)',
  colorWarning: 'var(--sst-color-warning)',
  colorAlert: 'var(--sst-color-alert)',
  colorInfo: 'var(--sst-color-info)',
  colorRailBackground: 'var(--sst-color-rail-background)',
  colorRailBorder: 'var(--sst-color-rail-border)',
  colorRailInk: 'var(--sst-color-rail-ink)',
  colorRailInkMuted: 'var(--sst-color-rail-ink-muted)',
  colorRailActiveBackground: 'var(--sst-color-rail-active-background)',
  colorRailActiveInk: 'var(--sst-color-rail-active-ink)',
  // Administração continua um botão sólido de destaque (não a mesma pílula do item ativo) — precisa
  // do próprio par de cores pra continuar "chamando atenção" nos dois temas.
  colorAdminButtonBackground: 'var(--sst-color-admin-button-background)',
  colorAdminButtonInk: 'var(--sst-color-admin-button-ink)',
  colorAdminButtonBackgroundHover: 'var(--sst-color-admin-button-background-hover)',
  // Sistema de cartões/KPIs (Hub Gênesis SST — design decidido em sessão anterior).
  colorCardBorder: 'var(--sst-color-card-border)',
  cardShadow: 'var(--sst-card-shadow)',
  colorSuccessWash: 'var(--sst-color-success-wash)',
  colorWarningWash: 'var(--sst-color-warning-wash)',
  colorAlertWash: 'var(--sst-color-alert-wash)',
  colorInfoWash: 'var(--sst-color-info-wash)',
  // Fundo de painel/card elevado e fundo da página por trás dos cards — mudam de branco/cinza claro
  // (tema claro) pra tons escuros (tema escuro).
  colorSurface: 'var(--sst-color-surface)',
  colorPageBackground: 'var(--sst-color-page-background)',
};

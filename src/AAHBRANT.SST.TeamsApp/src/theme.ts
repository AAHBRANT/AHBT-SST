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
// Sobrescritas comuns aos dois temas (spec 2026-09-07 §1.2 e §1.4): escala tipográfica de 6 passos
// e raios sm/md/lg ligados nos tokens do Fluent, para Text/Button/Input/Dialog seguirem sem wrapper.
const sobrescritasComuns: Partial<Theme> = {
  fontFamilyBase: "'Montserrat', -apple-system, BlinkMacSystemFont, sans-serif",
  fontSizeBase200: '11px', lineHeightBase200: '14px',
  fontSizeBase300: '14px', lineHeightBase300: '20px',
  fontSizeBase400: '16px', lineHeightBase400: '22px',
  fontSizeBase500: '20px', lineHeightBase500: '26px',
  fontSizeBase600: '28px', lineHeightBase600: '32px',
  fontWeightRegular: 500,
  fontWeightSemibold: 600,
  fontWeightBold: 700,
  borderRadiusSmall: '6px',
  borderRadiusMedium: '10px',
  borderRadiusLarge: '12px',
  borderRadiusXLarge: '12px',
};

export const aahbrantTheme: Theme = {
  ...createDarkTheme(aahbrantBrandRamp),
  ...sobrescritasComuns,
  colorNeutralBackground1: '#1E293B',
  colorNeutralBackground2: '#0F172A',
};

export const aahbrantLightTheme: Theme = {
  ...createLightTheme(aahbrantBrandRamp),
  ...sobrescritasComuns,
  colorNeutralBackground1: '#FFFFFF',
  colorNeutralBackground2: '#F5F5F7',
};

import { createLightTheme, type BrandVariants, type Theme } from '@fluentui/react-components';

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

export const aahbrantTheme: Theme = {
  ...createLightTheme(aahbrantBrandRamp),
  colorNeutralBackground1: '#FFFFFF',
  colorNeutralBackground2: '#F5F5F7',
  fontFamilyBase: "'Montserrat', -apple-system, BlinkMacSystemFont, sans-serif",
};

export const designTokens = {
  colorPrimary: '#670000',
  colorSecondary: '#7A0101',
  colorNeutralDark: '#1F1F1F',
  colorNeutralMedium: '#6D6D6D',
  colorNeutralLight: '#F5F5F7',
  colorWhite: '#FFFFFF',
  colorSuccess: '#16A34A',
  colorWarning: '#F59E0B',
  colorAlert: '#EF4444',
  colorInfo: '#3B82F6',
  // Rail de navegação — verde claro e sólido, texto em negrito de alto contraste (pedido do
  // usuário, 02/09, a partir de referência visual): sem gradiente translúcido nem rótulo
  // "esmaecido" no item não-selecionado — todo item já vem legível de cara.
  colorRailBackground: '#E8F5E9',
  colorRailBorder: '#C8E6C9',
  colorRailInk: '#15625C',
  colorRailInkMuted: '#15625C',
  colorRailActiveBackground: '#1B9B48',
  colorRailActiveInk: '#FFFFFF',
  // Sistema de cartões/KPIs (Hub Gênesis SST — design decidido em sessão anterior).
  colorCardBorder: '#E7E4DA',
  cardShadow: '0 1px 2px rgba(20,17,15,0.04), 0 6px 20px rgba(20,17,15,0.06)',
  colorSuccessWash: '#EAF7EE',
  colorWarningWash: '#FDF3E3',
  colorAlertWash: '#FCEAEA',
  colorInfoWash: '#EAF1FE',
};

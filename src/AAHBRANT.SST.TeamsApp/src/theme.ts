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
  // Rail de navegação — verde translúcido representando SST/segurança.
  colorRailBackground: 'linear-gradient(180deg, rgba(16,163,90,0.16), rgba(16,163,90,0.05))',
  colorRailBorder: 'rgba(15,131,73,0.28)',
  colorRailInk: '#0F5132',
  colorRailInkMuted: 'rgba(15,81,50,0.55)',
};

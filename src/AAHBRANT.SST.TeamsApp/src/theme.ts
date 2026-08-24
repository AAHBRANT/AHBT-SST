import { createLightTheme, type BrandVariants, type Theme } from '@fluentui/react-components';

// Rampa de marca gerada a partir do #7B1E2B (Design System AAHBRANT — DESING SYSTEM AAHBRANT.md).
const aahbrantBrandRamp: BrandVariants = {
  10: '#050203',
  20: '#210608',
  30: '#38090D',
  40: '#4C0C11',
  50: '#611016',
  60: '#76141B',
  70: '#7B1E2B', // color-primary
  80: '#8F2530',
  90: '#9E2A37', // color-secondary
  100: '#AD343F',
  110: '#BC4048',
  120: '#CB4D51',
  130: '#D95C5A',
  140: '#E66D64',
  150: '#F2806F',
  160: '#FC957B',
};

export const aahbrantTheme: Theme = {
  ...createLightTheme(aahbrantBrandRamp),
  colorNeutralBackground1: '#FFFFFF',
  colorNeutralBackground2: '#F5F5F7',
};

export const designTokens = {
  colorPrimary: '#7B1E2B',
  colorSecondary: '#9E2A37',
  colorNeutralDark: '#1F1F1F',
  colorNeutralMedium: '#6D6D6D',
  colorNeutralLight: '#F5F5F7',
  colorWhite: '#FFFFFF',
  colorSuccess: '#16A34A',
  colorWarning: '#F59E0B',
  colorAlert: '#EF4444',
  colorInfo: '#3B82F6',
};

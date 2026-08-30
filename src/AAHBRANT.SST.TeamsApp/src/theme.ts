import { createLightTheme, type BrandVariants, type Theme } from '@fluentui/react-components';

// Rampa de marca gerada a partir do vinho/marsala oficial da AAHBRANT (#670000, hover #7A0101 —
// tokens confirmados no ADOS/identidade-aahbrant). Os dois valores nos degraus 70 e 90 são os
// hex oficiais; os demais degraus são interpolados a partir deles (preto -> vinho -> rosa claro,
// sempre no mesmo matiz vermelho, sem derivar para laranja) só para dar suporte visual ao Fluent UI
// — não são cores oficiais novas. Substitui a rampa anterior baseada em #7B1E2B, que não confere
// com o manual de marca (ver DESING SYSTEM AAHBRANT.md, pendente de atualização).
const aahbrantBrandRamp: BrandVariants = {
  10: '#0F0000',
  20: '#1D0000',
  30: '#2C0000',
  40: '#3B0000',
  50: '#4A0000',
  60: '#580000',
  70: '#670000', // color-primary (vinho/marsala oficial)
  80: '#710101',
  90: '#7A0101', // color-secondary (vinho claro / hover oficial)
  100: '#8A1D1D',
  110: '#9B3939',
  120: '#AB5555',
  130: '#BB7070',
  140: '#CB8C8C',
  150: '#DCA8A8',
  160: '#ECC4C4',
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
  // Sidebar (rail) de navegação: verde translúcido, remetendo a segurança e saúde do
  // trabalho — pedido do usuário em 30/08, substitui a sidebar sólida em colorNeutralDark.
  colorRailBackground: 'linear-gradient(180deg, rgba(16,163,90,0.16), rgba(16,163,90,0.05))',
  colorRailBorder: 'rgba(15,131,73,0.28)',
  colorRailInk: '#0F5132',
  colorRailInkMuted: 'rgba(15,81,50,0.55)',
};

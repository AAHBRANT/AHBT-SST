import { makeStyles, shorthands } from '@fluentui/react-components';
import { designTokens, tokensUi } from '../../tokens/tokens';

const base = {
  '& .fui-Tab::before': { display: 'none' },
  '& .fui-Tab::after': { display: 'none' },
  '& .fui-Tab__content': { color: 'inherit' },
  '& .fui-Tab__icon': { color: 'inherit' },
};

export const useAbasStyles = makeStyles({
  pilar: {
    display: 'flex', flexWrap: 'wrap', columnGap: '2px', rowGap: 0, paddingLeft: '2px',
    ...shorthands.borderBottom('1px', 'solid', designTokens.colorCardBorder),
    '& .fui-Tab': {
      backgroundColor: designTokens.colorNeutralLight, ...shorthands.border('1px', 'solid', designTokens.colorCardBorder), borderBottom: 'none',
      borderRadius: `${tokensUi.raio.sm} ${tokensUi.raio.sm} 0 0`, color: designTokens.colorNeutralMedium, fontWeight: 600, fontSize: '13px',
      ...shorthands.padding('10px', '20px'), minHeight: 'auto', whiteSpace: 'nowrap', position: 'relative', top: '1px',
      transitionProperty: 'background-color, color', transitionDuration: '120ms',
    },
    '& .fui-Tab:hover': { backgroundColor: tokensUi.status.ok.fundo, color: tokensUi.status.ok.tinta },
    // Traço vinho no topo: único lugar em que a marca sela seleção (spec §1.1, aprovado 02/09).
    '& .fui-Tab[aria-selected="true"]': { backgroundColor: designTokens.colorSurface, borderTop: `2px solid ${designTokens.colorPrimary}`, paddingTop: '9px', color: designTokens.colorPrimary, fontWeight: 700 },
    '& .fui-Tab[aria-selected="true"]:hover': { backgroundColor: designTokens.colorSurface, color: designTokens.colorPrimary },
    ...base,
  },
  modulo: {
    display: 'flex', flexWrap: 'wrap', gap: '6px', marginTop: '18px', marginBottom: '20px', ...shorthands.padding('8px', '10px'),
    borderRadius: tokensUi.raio.md, backgroundColor: designTokens.colorNeutralLight,
    '& .fui-Tab': {
      backgroundColor: 'transparent', ...shorthands.border('1px', 'solid', 'transparent'), borderRadius: tokensUi.raio.full,
      color: designTokens.colorNeutralMedium, fontWeight: 600, fontSize: '12px', ...shorthands.padding('6px', '14px'), minHeight: 'auto', whiteSpace: 'nowrap',
      transitionProperty: 'background-color, color', transitionDuration: '200ms',
    },
    '& .fui-Tab:hover': { backgroundColor: tokensUi.status.ok.fundo, color: tokensUi.status.ok.tinta },
    // Seleção = chrome (spec §1.1): sólido, família chrome, não colorSuccess.
    '& .fui-Tab[aria-selected="true"]': { backgroundColor: tokensUi.chrome.ativoFundo, color: tokensUi.chrome.ativoTinta, fontWeight: 700 },
    '& .fui-Tab[aria-selected="true"]:hover': { backgroundColor: tokensUi.chrome.ativoFundo, color: tokensUi.chrome.ativoTinta },
    ...base,
  },
  interno: {
    display: 'flex', flexWrap: 'wrap', gap: '4px', marginBottom: tokensUi.espaco.lg,
    '& .fui-Tab': {
      backgroundColor: 'transparent', ...shorthands.border('1px', 'solid', designTokens.colorCardBorder), borderRadius: tokensUi.raio.full,
      color: designTokens.colorNeutralMedium, fontWeight: 600, fontSize: '11px', ...shorthands.padding('4px', '10px'), minHeight: 'auto', whiteSpace: 'nowrap',
    },
    '& .fui-Tab[aria-selected="true"]': { backgroundColor: designTokens.colorSurface, ...shorthands.borderColor(tokensUi.chrome.ativoFundo), color: tokensUi.chrome.ativoFundo, fontWeight: 700 },
    ...base,
  },
  contador: { marginLeft: '6px', opacity: 0.7, fontVariantNumeric: 'tabular-nums' },
});

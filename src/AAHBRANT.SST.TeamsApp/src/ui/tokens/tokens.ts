import { designTokens } from '../../theme';

// Tokens da camada ui/ (spec 2026-09-07 §1.1, §1.3, §1.4). Referenciam CSS custom properties de
// index.css — mesma razão de designTokens: Griffel gera as classes uma vez, e var() permite trocar
// de tema só mudando data-theme na raiz. Re-exporta designTokens para a camada ter uma porta só.
export { designTokens };

export const tokensUi = {
  chrome: {
    fundo: 'var(--sst-chrome-fundo)',
    borda: 'var(--sst-chrome-borda)',
    tinta: 'var(--sst-chrome-tinta)',
    ativoFundo: 'var(--sst-chrome-ativo-fundo)',
    ativoTinta: 'var(--sst-chrome-ativo-tinta)',
  },
  status: {
    ok: { tinta: 'var(--sst-status-ok-tinta)', fundo: 'var(--sst-status-ok-fundo)' },
    atencao: { tinta: 'var(--sst-status-atencao-tinta)', fundo: 'var(--sst-status-atencao-fundo)' },
    alerta: { tinta: 'var(--sst-status-alerta-tinta)', fundo: 'var(--sst-status-alerta-fundo)' },
    info: { tinta: 'var(--sst-status-info-tinta)', fundo: 'var(--sst-status-info-fundo)' },
    neutro: { tinta: 'var(--sst-status-neutro-tinta)', fundo: 'var(--sst-status-neutro-fundo)' },
  },
  bordaSuave: 'var(--sst-border-soft)',
  sombraLinha: 'var(--sst-row-shadow)',
  sombraLinhaHover: 'var(--sst-row-shadow-hover)',
  espaco: { xs: '4px', sm: '8px', md: '12px', lg: '16px', xl: '24px', xxl: '32px', xxxl: '48px' },
  raio: { sm: '6px', md: '10px', lg: '12px', full: '999px' },
  // Durações e curva das transições CSS (spec §1.5). movimento.ts guarda os equivalentes em segundos
  // para o framer-motion; aqui ficam os valores em ms usados por Griffel/transitionDuration.
  duracao: { rapido: '120ms', normal: '200ms', entrada: '300ms' },
  curva: 'cubic-bezier(0.2, 0, 0, 1)',
} as const;

export type Tom = keyof typeof tokensUi.status;

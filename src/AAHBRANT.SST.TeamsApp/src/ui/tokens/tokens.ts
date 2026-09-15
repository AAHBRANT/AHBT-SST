// Tokens de cor usados pelos estilos próprios do app (fora dos componentes nativos do Fluent, que já
// respondem à troca de tema sozinhos via aahbrantTheme/aahbrantLightTheme em ../../theme). Referenciam
// variáveis CSS (ver index.css) em vez de valores fixos: os estilos deste app usam Griffel
// makeStyles, que gera as classes CSS uma única vez — se os valores aqui fossem string fixa, trocar
// de tema não teria efeito nenhum sem recriar todas as classes. Com var(), o mesmo CSS gerado já
// aponta pra variável, e o botão de dark/light mode só precisa trocar o atributo data-theme na raiz
// (ver ThemeModeContext.tsx) pra tudo atualizar junto, sem re-render.
// Movido de src/theme.ts pra cá na Onda 3 (Task 24): theme.ts passou a ser só o tema base que o
// FluentProvider consome direto, sem servir de bridge pra páginas/componentes — esta é a porta única.
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
  // Ainda em uso real por src/layout/AppShell.tsx (rail de navegação e botão de Administração) —
  // confirmado com grep na Onda 3 (Task 24). Não confundir com --sst-chrome-* (tokens.chrome abaixo),
  // que cobre o mesmo conceito para consumidores de src/ui/; os dois convivem porque AppShell.tsx é
  // chrome do app (fora de src/pages/), não uma peça migrada por esta frente.
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

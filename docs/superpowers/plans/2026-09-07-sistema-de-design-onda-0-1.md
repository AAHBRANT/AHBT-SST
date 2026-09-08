# Sistema de Design — Ondas 0 e 1 — Plano de Implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Criar a camada `src/ui/` completa (Onda 0, aditiva, nenhuma página muda) e migrar três telas-piloto para ela (Onda 1), com galeria dev e snapshots visuais.

**Architecture:** Fluent UI v9 continua como motor de comportamento/acessibilidade. `src/ui/` expõe ~18 componentes compostos que consomem tokens CSS (`--sst-*`) e Griffel. Páginas passam a importar de `@ui`; lint impede import direto do Fluent em `pages/`. Nada é movido nesta fase — `ui/` re-exporta o que já existe em `theme.ts`, `components/`, `hooks/`; mover fica para a Onda 3 (Plano B).

**Tech Stack:** React 19.2, Fluent UI React Components 9.74, Griffel (`makeStyles`), framer-motion 13, react-router-dom 7 (HashRouter), Vite 8, TypeScript 6, oxlint 1.75, Playwright (novo, devDependency).

**Spec:** `docs/superpowers/specs/2026-09-07-sistema-de-design-design.md`

## Global Constraints

- Diretório de trabalho de todos os comandos: `C:/Projetos/SST-APP/src/AAHBRANT.SST.TeamsApp` (salvo indicação). Git roda na raiz `C:/Projetos/SST-APP`.
- Branch de trabalho: criar `feature/ui-onda-0` a partir de `master` antes da Tarefa 1; Onda 1 usa uma branch por piloto (`feature/ui-piloto-entregas`, `feature/ui-piloto-nc`, `feature/ui-piloto-matriz-epi`) criada a partir de `master` após o merge da Onda 0.
- **Onda 0 é aditiva:** nenhum arquivo em `src/pages/`, `src/components/`, `src/hooks/`, `src/layout/` é modificado, exceto `App.tsx` (rota da galeria) e `index.css`/`theme.ts` (tokens novos ao lado dos antigos). Nada é movido ou apagado.
- Verificação por tarefa (não há test runner no frontend; a spec exclui testes unitários nesta frente): `npx tsc -b` sem erros → `npx oxlint` sem erros novos → item visível na galeria `/#/ui-galeria` nos dois temas → commit. A partir da Tarefa 14, também `npm run ui:snapshots`.
- Tokens exatos (spec §1): tipografia `display 28/800/32`, `titulo 20/700/26`, `subtitulo 16/600/22`, `corpo 14/500/20`, `legenda 12/600/16`, `micro 11/700/14 uppercase .05em`; espaçamento `4 8 12 16 24 32 48`; raio `6 10 12 999`; movimento `120ms 200ms 300ms`, curva `cubic-bezier(0.2, 0, 0, 1)`.
- Cores chrome (claro/escuro): fundo `#e8f5e9`/`#1e293b`, borda `#c8e6c9`/`#334155`, tinta `#15625c`/`#9ca3af`, ativo-fundo `#1b9b48`/`#064e3b`, ativo-tinta `#ffffff`/`#10b981`. Status: ok `#16a34a`, atencao `#f59e0b`, alerta `#ef4444`, info `#3b82f6`, neutro `#6d6d6d`/`#9ca3af`; fundos lavados já existem como `--sst-color-*-wash`.
- Nomes em português; props em português (`aoMudar`, `carregando`, `tom`). `StatusChip` e `KpiCard` mantêm o nome.
- Um componente por pasta: `src/ui/<grupo>/<Nome>/<Nome>.tsx`, estilos em `<Nome>.styles.ts` quando passarem de ~40 linhas.
- Comentário de cabeçalho em todo componente novo: uma frase de propósito + o padrão que substitui.
- Hex cru proibido fora de `index.css` e `theme.ts`.
- Mensagens de commit em português, prefixo `feat(ui):`, `chore(ui):`, `refactor(epi):` etc. Terminar com `Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>`.

---

## Onda 0 — Fundação

### Task 1: Alias `@ui`, tokens novos e esqueleto de `src/ui/`

**Files:**
- Modify: `tsconfig.app.json` (compilerOptions)
- Modify: `vite.config.ts` (resolve.alias)
- Modify: `src/index.css:27-72` (tokens chrome/status/row-shadow ao lado dos antigos)
- Modify: `src/theme.ts:26-38` (raios e tipografia no tema Fluent)
- Create: `src/ui/tokens/tokens.ts`
- Create: `src/ui/tokens/tipografia.ts`
- Create: `src/ui/tokens/movimento.ts`
- Create: `src/ui/index.ts`

**Interfaces:**
- Produces: `tokensUi` (objeto com `chrome.*`, `status.*`, `espaco.*`, `raio.*` como strings `var(--sst-…)`), `useTipografia()` (classes `display|titulo|subtitulo|corpo|legenda|micro`), `duracao`, `curva`, presets `entrada`, `escalonado(i)`, `deslizarDe(lado)`. Re-exporta `designTokens` de `../../theme`.

- [ ] **Step 1: Criar a branch**

```bash
cd C:/Projetos/SST-APP && git checkout master && git pull --ff-only && git checkout -b feature/ui-onda-0
```

- [ ] **Step 2: Alias no TypeScript**

Em `tsconfig.app.json`, dentro de `compilerOptions`, adicionar após `"jsx": "react-jsx",`:

```json
    "baseUrl": ".",
    "paths": {
      "@ui": ["src/ui/index.ts"],
      "@ui/*": ["src/ui/*"]
    },
```

- [ ] **Step 3: Alias no Vite**

Em `vite.config.ts`, adicionar `import path from 'node:path'` no topo e, dentro de `defineConfig({ … })`, antes de `server:`:

```ts
  resolve: {
    alias: {
      '@ui': path.resolve(__dirname, 'src/ui/index.ts'),
      '@ui/': path.resolve(__dirname, 'src/ui') + '/',
    },
  },
```

- [ ] **Step 4: Tokens novos em `index.css`**

Dentro do bloco `:root, [data-theme='light'] { … }` (linhas 27–49), acrescentar antes do `}`:

```css
  /* Famílias novas (spec 2026-09-07 §1.1). Convivem com --sst-color-rail-* até a Onda 3. */
  --sst-chrome-fundo: #e8f5e9;
  --sst-chrome-borda: #c8e6c9;
  --sst-chrome-tinta: #15625c;
  --sst-chrome-ativo-fundo: #1b9b48;
  --sst-chrome-ativo-tinta: #ffffff;
  --sst-status-ok-tinta: #16a34a;      --sst-status-ok-fundo: #eaf7ee;
  --sst-status-atencao-tinta: #d97706; --sst-status-atencao-fundo: #fdf3e3;
  --sst-status-alerta-tinta: #dc2626;  --sst-status-alerta-fundo: #fceaea;
  --sst-status-info-tinta: #2563eb;    --sst-status-info-fundo: #eaf1fe;
  --sst-status-neutro-tinta: #6d6d6d;  --sst-status-neutro-fundo: #f0f0f2;
  --sst-border-soft: #efede6;
  --sst-row-shadow: 0 1px 2px rgba(20, 17, 15, 0.05);
  --sst-row-shadow-hover: 0 4px 12px rgba(20, 17, 15, 0.10);
```

Dentro de `[data-theme='dark'] { … }` (linhas 51–72), acrescentar antes do `}`:

```css
  --sst-chrome-fundo: #1e293b;
  --sst-chrome-borda: #334155;
  --sst-chrome-tinta: #9ca3af;
  --sst-chrome-ativo-fundo: #064e3b;
  --sst-chrome-ativo-tinta: #10b981;
  --sst-status-ok-tinta: #16a34a;      --sst-status-ok-fundo: rgba(22, 163, 74, 0.16);
  --sst-status-atencao-tinta: #f59e0b; --sst-status-atencao-fundo: rgba(245, 158, 11, 0.16);
  --sst-status-alerta-tinta: #ef4444;  --sst-status-alerta-fundo: rgba(239, 68, 68, 0.16);
  --sst-status-info-tinta: #3b82f6;    --sst-status-info-fundo: rgba(59, 130, 246, 0.16);
  --sst-status-neutro-tinta: #9ca3af;  --sst-status-neutro-fundo: #28394f;
  --sst-border-soft: #2a3a52;
  --sst-row-shadow: 0 1px 2px rgba(0, 0, 0, 0.25);
  --sst-row-shadow-hover: 0 6px 16px rgba(0, 0, 0, 0.4);
```

Ao final do arquivo, acrescentar:

```css
/* Movimento: com "reduzir movimento" no sistema operacional, tudo é instantâneo (spec §1.5). */
@media (prefers-reduced-motion: reduce) {
  *, *::before, *::after {
    animation-duration: 0s !important;
    animation-delay: 0s !important;
    transition-duration: 0s !important;
  }
}
```

- [ ] **Step 5: Raios e tipografia no tema Fluent**

Em `src/theme.ts`, substituir os dois objetos exportados (linhas 26–38) por:

```ts
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
```

- [ ] **Step 6: `src/ui/tokens/tokens.ts`**

```ts
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
} as const;

export type Tom = keyof typeof tokensUi.status;
```

- [ ] **Step 7: `src/ui/tokens/tipografia.ts`**

```ts
import { makeStyles } from '@fluentui/react-components';

// Os seis passos tipográficos (spec §1.2). Componentes de ui/ usam estas classes; páginas usam os
// componentes. Fora daqui, tamanho de fonte à mão é proibido.
export const useTipografia = makeStyles({
  display: { fontSize: '28px', lineHeight: '32px', fontWeight: 800, letterSpacing: '-0.01em', fontVariantNumeric: 'tabular-nums' },
  titulo: { fontSize: '20px', lineHeight: '26px', fontWeight: 700 },
  subtitulo: { fontSize: '16px', lineHeight: '22px', fontWeight: 600 },
  corpo: { fontSize: '14px', lineHeight: '20px', fontWeight: 500 },
  legenda: { fontSize: '12px', lineHeight: '16px', fontWeight: 600 },
  micro: { fontSize: '11px', lineHeight: '14px', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.05em' },
});
```

- [ ] **Step 8: `src/ui/tokens/movimento.ts`**

```ts
import type { Transition, Variants } from 'framer-motion';

// Movimento (spec §1.5): três durações, uma curva, dois motivos — chegada (montagem) e origem
// (algo abre de onde foi acionado). Páginas nunca escrevem transition={{ duration }} à mão.
export const duracao = { rapido: 0.12, normal: 0.2, entrada: 0.3 } as const;
export const curva: [number, number, number, number] = [0.2, 0, 0, 1];

export const transicaoEntrada: Transition = { duration: duracao.entrada, ease: curva };
export const transicaoNormal: Transition = { duration: duracao.normal, ease: curva };

export const entrada: Variants = {
  inicial: { opacity: 0, y: 8 },
  visivel: { opacity: 1, y: 0, transition: transicaoEntrada },
};

export function escalonado(indice: number): Variants {
  return {
    inicial: { opacity: 0, y: 8 },
    visivel: { opacity: 1, y: 0, transition: { ...transicaoEntrada, delay: indice * 0.04 } },
  };
}

export function deslizarDe(lado: 'direita' | 'baixo'): Variants {
  const fora = lado === 'direita' ? { x: '100%' } : { y: '100%' };
  return {
    fechado: { ...fora, transition: transicaoEntrada },
    aberto: { x: 0, y: 0, transition: transicaoEntrada },
  };
}
```

- [ ] **Step 9: `src/ui/index.ts` (porta única; cresce a cada tarefa)**

```ts
// Porta única da camada ui/ (spec 2026-09-07 §2). Páginas importam daqui, nunca do Fluent direto.
export * from './tokens/tokens';
export * from './tokens/tipografia';
export * from './tokens/movimento';

// Primitivos Fluent que não ganham wrapper (spec §2.2) — re-exportados para a página não precisar
// importar @fluentui/react-components.
export {
  Avatar, Button, Checkbox, Field, Input, Select, Spinner, Text, Textarea, Tooltip,
} from '@fluentui/react-components';

// Peças já existentes, ainda no lugar antigo até a Onda 3.
export { CampoData } from '../components/CampoData';
export { ChipsField } from '../components/ChipsField';
```

- [ ] **Step 10: Verificar**

```bash
npx tsc -b && npx oxlint
```
Esperado: sem erros.

- [ ] **Step 11: Commit**

```bash
cd C:/Projetos/SST-APP && git add src/AAHBRANT.SST.TeamsApp/tsconfig.app.json src/AAHBRANT.SST.TeamsApp/vite.config.ts src/AAHBRANT.SST.TeamsApp/src/index.css src/AAHBRANT.SST.TeamsApp/src/theme.ts src/AAHBRANT.SST.TeamsApp/src/ui && git commit -m "feat(ui): alias @ui, tokens chrome/status e escalas no tema Fluent

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 2: Gate de lint (`warn`)

**Files:**
- Modify: `.oxlintrc.json`

- [ ] **Step 1: Substituir o arquivo**

```json
{
  "$schema": "./node_modules/oxlint/configuration_schema.json",
  "plugins": ["react", "typescript", "oxc"],
  "rules": {
    "react/rules-of-hooks": "error",
    "react/only-export-components": ["warn", { "allowConstantExport": true }]
  },
  "overrides": [
    {
      "files": ["src/pages/**"],
      "rules": {
        "no-restricted-imports": ["warn", {
          "paths": [
            { "name": "@fluentui/react-components", "message": "Em pages/, importe de @ui (spec 2026-09-07 §2.2)." }
          ],
          "patterns": [
            { "group": ["**/theme", "**/theme.ts"], "message": "Tokens só via componentes de @ui." }
          ]
        }]
      }
    },
    {
      "files": ["src/ui/**"],
      "rules": {
        "no-restricted-imports": ["error", {
          "patterns": [
            { "group": ["**/lib/api", "**/lib/api.ts"], "message": "ui/ não busca dados; recebe por props." },
            { "group": ["**/pages/**"], "message": "ui/ não conhece páginas." }
          ]
        }]
      }
    }
  ]
}
```

- [ ] **Step 2: Verificar que o gate dispara**

```bash
npx oxlint 2>&1 | grep -c "no-restricted-imports"
```
Esperado: número > 100 (todas as páginas atuais avisam — é o comportamento desejado na Onda 0). Saída total ainda `0 errors`.

- [ ] **Step 3: Commit**

```bash
cd C:/Projetos/SST-APP && git add src/AAHBRANT.SST.TeamsApp/.oxlintrc.json && git commit -m "chore(ui): gate de lint no-restricted-imports em warn

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 3: Galeria dev `/ui-galeria` (vazia) + rota

**Files:**
- Create: `src/ui/galeria/GaleriaPage.tsx`
- Create: `src/ui/galeria/Secao.tsx`
- Modify: `src/App.tsx:100-101` (rota condicional)

**Interfaces:**
- Produces: `<Secao titulo>children</Secao>` para as tarefas seguintes adicionarem exemplos; `GaleriaPage` renderiza `secoes` em ordem.

- [ ] **Step 1: `Secao.tsx`**

```tsx
import type { ReactNode } from 'react';
import { makeStyles } from '@fluentui/react-components';
import { tokensUi } from '../tokens/tokens';
import { useTipografia } from '../tokens/tipografia';

const useStyles = makeStyles({
  root: { marginBottom: tokensUi.espaco.xxl },
  titulo: { marginBottom: tokensUi.espaco.md, paddingBottom: tokensUi.espaco.sm, borderBottom: `1px solid ${tokensUi.bordaSuave}` },
  corpo: { display: 'flex', flexWrap: 'wrap', gap: tokensUi.espaco.lg, alignItems: 'flex-start' },
});

// Bloco da galeria: título + exemplos de uma peça lado a lado.
export function Secao({ titulo, children }: { titulo: string; children: ReactNode }) {
  const estilos = useStyles();
  const tipo = useTipografia();
  return (
    <section className={estilos.root}>
      <h2 className={`${tipo.subtitulo} ${estilos.titulo}`}>{titulo}</h2>
      <div className={estilos.corpo}>{children}</div>
    </section>
  );
}
```

- [ ] **Step 2: `GaleriaPage.tsx`**

```tsx
import { useTipografia } from '../tokens/tipografia';
import { Secao } from './Secao';

// Galeria viva da camada ui/ (spec §6): cada peça em todos os estados, nos dois temas (use o botão
// da topbar). Só existe em desenvolvimento — ver a rota condicional em App.tsx. É a documentação.
export function GaleriaPage() {
  const tipo = useTipografia();
  return (
    <div>
      <h1 className={tipo.titulo} style={{ marginBottom: 24 }}>Galeria da camada ui/</h1>
      <Secao titulo="Tipografia">
        <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
          <span className={tipo.display}>1.248</span>
          <span className={tipo.titulo}>Entregas de EPI</span>
          <span className={tipo.subtitulo}>Plano de ação</span>
          <span className={tipo.corpo}>Andaime sem guarda-corpo no pavimento 3.</span>
          <span className={tipo.legenda}>Pedreiro, Ponte Rio Cuiá</span>
          <span className={tipo.micro}>Vence em breve</span>
        </div>
      </Secao>
      {/* As tarefas seguintes acrescentam uma <Secao> por peça, nesta ordem: StatusChip, Card,
          PageHeader, EstadoVazio, Carregando, FeedbackInline, Abas, DataTable, FormSection/FormGrid,
          ChipCheckboxGroup, SeletorPesquisavel, ConfirmDialog, PainelLateral, KpiCard,
          DetailPageLayout, WorkflowActions, Gráficos. */}
    </div>
  );
}
```

- [ ] **Step 3: Rota em `App.tsx`**

Adicionar import no topo: `import { GaleriaPage } from './ui/galeria/GaleriaPage';`

Dentro de `<Route element={<LayoutComTeams />}>`, logo após `<Route path="/" element={<DashboardPage />} />`:

```tsx
            {/* Galeria da camada ui/ (spec 2026-09-07 §6) — só em desenvolvimento. */}
            {import.meta.env.DEV && <Route path="/ui-galeria" element={<GaleriaPage />} />}
```

- [ ] **Step 4: Verificar**

```bash
npx tsc -b && npx oxlint && npm run dev
```
Abrir `http://localhost:5173/#/ui-galeria`. Esperado: título e os seis passos tipográficos. Alternar tema pela topbar: cores mudam, tamanhos não.

- [ ] **Step 5: Commit**

```bash
cd C:/Projetos/SST-APP && git add src/AAHBRANT.SST.TeamsApp/src/ui/galeria src/AAHBRANT.SST.TeamsApp/src/App.tsx && git commit -m "feat(ui): galeria dev /ui-galeria com escala tipográfica

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 4: `StatusChip` + `nivelVencimento`

**Files:**
- Create: `src/ui/primitivos/StatusChip/StatusChip.tsx`
- Create: `src/ui/primitivos/StatusChip/vencimento.ts`
- Modify: `src/ui/index.ts`
- Modify: `src/ui/galeria/GaleriaPage.tsx`

**Interfaces:**
- Produces: `StatusChip({ tom: Tom; icone?: ReactElement; children: ReactNode; className?: string })`; `nivelVencimento(dataValidade?: string | null): NivelVencimento | null` com `NivelVencimento = 'vencido' | 'alerta' | 'valido'`; `tomDeVencimento(nivel): Tom`; `rotuloDeVencimento(nivel): string`; `LIMIAR_ALERTA_DIAS = 30`.

- [ ] **Step 1: `vencimento.ts`** (regra dos 30 dias preservada de `components/badges/BadgeVencimento.tsx`)

```ts
import type { Tom } from '../../tokens/tokens';

// Regra de vencimento em 3 níveis (PR-SST-003): vermelho = vencido, amarelo = vence em até 30 dias,
// verde = válido. Copiada de BadgeVencimento.tsx, que passa a ser substituído por StatusChip.
export const LIMIAR_ALERTA_DIAS = 30;

export type NivelVencimento = 'vencido' | 'alerta' | 'valido';

export function nivelVencimento(dataValidade?: string | null): NivelVencimento | null {
  if (!dataValidade) return null;
  const hoje = new Date(new Date().toDateString());
  const validade = new Date(dataValidade);
  const diasRestantes = Math.ceil((validade.getTime() - hoje.getTime()) / 86_400_000);
  if (diasRestantes < 0) return 'vencido';
  if (diasRestantes <= LIMIAR_ALERTA_DIAS) return 'alerta';
  return 'valido';
}

export function tomDeVencimento(nivel: NivelVencimento): Tom {
  return nivel === 'vencido' ? 'alerta' : nivel === 'alerta' ? 'atencao' : 'ok';
}

export function rotuloDeVencimento(nivel: NivelVencimento): string {
  return nivel === 'vencido' ? 'Vencido' : nivel === 'alerta' ? 'Vence em breve' : 'Válido';
}
```

- [ ] **Step 2: `StatusChip.tsx`**

```tsx
import type { ReactElement, ReactNode } from 'react';
import { makeStyles, mergeClasses } from '@fluentui/react-components';
import { tokensUi, type Tom } from '../../tokens/tokens';

const useStyles = makeStyles({
  root: {
    display: 'inline-flex', alignItems: 'center', gap: '5px', height: '22px', padding: '0 9px',
    borderRadius: tokensUi.raio.full, fontSize: '11px', lineHeight: '14px', fontWeight: 700,
    textTransform: 'uppercase', letterSpacing: '0.05em', whiteSpace: 'nowrap',
  },
  ponto: { width: '6px', height: '6px', borderRadius: '50%', backgroundColor: 'currentColor', flexShrink: 0 },
  ok: { color: tokensUi.status.ok.tinta, backgroundColor: tokensUi.status.ok.fundo },
  atencao: { color: tokensUi.status.atencao.tinta, backgroundColor: tokensUi.status.atencao.fundo },
  alerta: { color: tokensUi.status.alerta.tinta, backgroundColor: tokensUi.status.alerta.fundo },
  info: { color: tokensUi.status.info.tinta, backgroundColor: tokensUi.status.info.fundo },
  neutro: { color: tokensUi.status.neutro.tinta, backgroundColor: tokensUi.status.neutro.fundo },
});

export interface StatusChipProps {
  tom: Tom;
  icone?: ReactElement;
  children: ReactNode;
  className?: string;
}

// Chip de estado (spec §1.1, §3): sempre fundo lavado + tinta + ponto/ícone, nunca sólido — é o que
// o separa do verde de navegação. Substitui useStatusChipStyles, BadgeVencimento, <Badge color=> do
// Fluent e cores hex passadas por prop.
export function StatusChip({ tom, icone, children, className }: StatusChipProps) {
  const estilos = useStyles();
  return (
    <span className={mergeClasses(estilos.root, estilos[tom], className)}>
      {icone ?? <span className={estilos.ponto} aria-hidden="true" />}
      {children}
    </span>
  );
}
```

- [ ] **Step 3: Exportar em `src/ui/index.ts`**

```ts
export { StatusChip, type StatusChipProps } from './primitivos/StatusChip/StatusChip';
export * from './primitivos/StatusChip/vencimento';
```

- [ ] **Step 4: Galeria** — em `GaleriaPage.tsx`, importar `{ StatusChip, nivelVencimento, tomDeVencimento, rotuloDeVencimento }` de `'../index'` e adicionar após a seção Tipografia:

```tsx
      <Secao titulo="StatusChip">
        <StatusChip tom="ok">Vigente</StatusChip>
        <StatusChip tom="atencao">Vence em 8 dias</StatusChip>
        <StatusChip tom="alerta">Vencido</StatusChip>
        <StatusChip tom="info">Aguardando assinatura</StatusChip>
        <StatusChip tom="neutro">Prevista</StatusChip>
        {(() => { const n = nivelVencimento('2099-01-01')!; return <StatusChip tom={tomDeVencimento(n)}>{rotuloDeVencimento(n)} (helper)</StatusChip>; })()}
      </Secao>
```

- [ ] **Step 5: Verificar** — `npx tsc -b && npx oxlint`; na galeria, cinco chips lavados nos dois temas; o sexto lê "Válido (helper)" em verde.

- [ ] **Step 6: Commit**

```bash
cd C:/Projetos/SST-APP && git add src/AAHBRANT.SST.TeamsApp/src/ui && git commit -m "feat(ui): StatusChip e helper nivelVencimento

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 5: `Card` + `PageHeader`

**Files:**
- Create: `src/ui/compostos/Card/Card.tsx`
- Create: `src/ui/compostos/PageHeader/PageHeader.tsx`
- Modify: `src/ui/index.ts`, `src/ui/galeria/GaleriaPage.tsx`

**Interfaces:**
- Produces: `Card({ densidade?: 'confortavel' | 'compacta'; titulo?: ReactNode; subtitulo?: ReactNode; acoes?: ReactNode; className?: string; children })`; `PageHeader({ titulo: ReactNode; subtitulo?: ReactNode; status?: ReactNode; acoes?: ReactNode; filtros?: ReactNode; voltarPara?: string; rotuloVoltar?: string })`.

- [ ] **Step 1: `Card.tsx`**

```tsx
import type { ReactNode } from 'react';
import { makeStyles, mergeClasses } from '@fluentui/react-components';
import { designTokens, tokensUi } from '../../tokens/tokens';
import { useTipografia } from '../../tokens/tipografia';

const useStyles = makeStyles({
  root: {
    backgroundColor: designTokens.colorSurface, border: `1px solid ${designTokens.colorCardBorder}`,
    borderRadius: tokensUi.raio.lg, boxShadow: designTokens.cardShadow, padding: tokensUi.espaco.xl,
  },
  compacta: { padding: tokensUi.espaco.lg },
  topo: { display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: tokensUi.espaco.md, marginBottom: tokensUi.espaco.md },
  subtitulo: { color: designTokens.colorNeutralMedium, marginTop: '2px' },
});

export interface CardProps {
  densidade?: 'confortavel' | 'compacta';
  titulo?: ReactNode;
  subtitulo?: ReactNode;
  acoes?: ReactNode;
  className?: string;
  children: ReactNode;
}

// O card único do sistema (spec §1.4): raio lg, padding xl (lg na densa), uma sombra por tema.
// Substitui usePageStyles.card, useDashboardStyles.chartCard/motorPainel e cards ad hoc.
export function Card({ densidade = 'confortavel', titulo, subtitulo, acoes, className, children }: CardProps) {
  const estilos = useStyles();
  const tipo = useTipografia();
  const temTopo = titulo || subtitulo || acoes;
  return (
    <div className={mergeClasses(estilos.root, densidade === 'compacta' && estilos.compacta, className)}>
      {temTopo && (
        <div className={estilos.topo}>
          <div>
            {titulo && <div className={tipo.subtitulo}>{titulo}</div>}
            {subtitulo && <div className={mergeClasses(tipo.legenda, estilos.subtitulo)}>{subtitulo}</div>}
          </div>
          {acoes && <div>{acoes}</div>}
        </div>
      )}
      {children}
    </div>
  );
}
```

- [ ] **Step 2: `PageHeader.tsx`**

```tsx
import type { ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button, makeStyles, mergeClasses } from '@fluentui/react-components';
import { ArrowLeft16Regular } from '@fluentui/react-icons';
import { designTokens, tokensUi } from '../../tokens/tokens';
import { useTipografia } from '../../tokens/tipografia';

const useStyles = makeStyles({
  root: { display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: tokensUi.espaco.lg, marginBottom: tokensUi.espaco.lg, flexWrap: 'wrap' },
  titulo: { margin: 0, display: 'flex', alignItems: 'center', gap: tokensUi.espaco.sm, flexWrap: 'wrap' },
  subtitulo: { color: designTokens.colorNeutralMedium, marginTop: tokensUi.espaco.xs },
  acoes: { display: 'flex', alignItems: 'center', gap: tokensUi.espaco.sm, flexWrap: 'wrap' },
  voltar: { marginBottom: '6px', color: designTokens.colorNeutralMedium },
});

export interface PageHeaderProps {
  titulo: ReactNode;
  subtitulo?: ReactNode;
  status?: ReactNode;
  acoes?: ReactNode;
  filtros?: ReactNode;
  voltarPara?: string;
  rotuloVoltar?: string;
}

// Cabeçalho de página (spec §3): título + subtítulo à esquerda, filtros e ações à direita, link de
// voltar opcional acima. Substitui o par toolbar + <Text size={500} weight="semibold"> repetido em
// toda página e o boolean mostrarTitulo — a página-pilar simplesmente não renderiza PageHeader no filho.
export function PageHeader({ titulo, subtitulo, status, acoes, filtros, voltarPara, rotuloVoltar }: PageHeaderProps) {
  const estilos = useStyles();
  const tipo = useTipografia();
  const navigate = useNavigate();
  return (
    <div>
      {voltarPara && (
        <Button appearance="subtle" size="small" icon={<ArrowLeft16Regular />} className={estilos.voltar} onClick={() => navigate(voltarPara)}>
          {rotuloVoltar ?? 'Voltar'}
        </Button>
      )}
      <div className={estilos.root}>
        <div>
          <h1 className={mergeClasses(tipo.titulo, estilos.titulo)}>{titulo}{status}</h1>
          {subtitulo && <div className={mergeClasses(tipo.corpo, estilos.subtitulo)}>{subtitulo}</div>}
        </div>
        {(filtros || acoes) && <div className={estilos.acoes}>{filtros}{acoes}</div>}
      </div>
    </div>
  );
}
```

- [ ] **Step 3: Exportar** em `index.ts`:

```ts
export { Card, type CardProps } from './compostos/Card/Card';
export { PageHeader, type PageHeaderProps } from './compostos/PageHeader/PageHeader';
```

- [ ] **Step 4: Galeria** — adicionar (importando `Card`, `PageHeader`, `Button`, `StatusChip` de `'../index'`):

```tsx
      <Secao titulo="Card">
        <Card titulo="Confortável" subtitulo="Padding xl" style={{ width: 280 }}>Conteúdo</Card>
        <Card densidade="compacta" titulo="Compacto" acoes={<Button size="small">Ação</Button>}>Conteúdo</Card>
      </Secao>
      <Secao titulo="PageHeader">
        <div style={{ width: '100%' }}>
          <PageHeader titulo="Entregas de EPI" subtitulo="287 entregas ativas em 7 obras." status={<StatusChip tom="info">Em tratamento</StatusChip>} filtros={<Input placeholder="Buscar" />} acoes={<Button appearance="primary">Nova entrega</Button>} voltarPara="/ui-galeria" rotuloVoltar="Não conformidades" />
        </div>
      </Secao>
```
(`Card` não aceita `style`; para a galeria use `className` com uma classe local `makeStyles({ largura: { width: '280px' } })` — não use `style` inline.)

- [ ] **Step 5: Verificar** — `npx tsc -b && npx oxlint`; galeria mostra os dois cards e o cabeçalho com voltar.

- [ ] **Step 6: Commit**

```bash
cd C:/Projetos/SST-APP && git add src/AAHBRANT.SST.TeamsApp/src/ui && git commit -m "feat(ui): Card e PageHeader

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 6: `EstadoVazio`, `Carregando`, `FeedbackInline`

**Files:**
- Create: `src/ui/primitivos/EstadoVazio/EstadoVazio.tsx`
- Create: `src/ui/primitivos/Carregando/Carregando.tsx`
- Create: `src/ui/primitivos/FeedbackInline/FeedbackInline.tsx`
- Modify: `src/ui/index.ts`, `src/ui/galeria/GaleriaPage.tsx`

**Interfaces:**
- Produces: `EstadoVazio({ icone?: ReactElement; titulo: ReactNode; descricao?: ReactNode; acao?: { rotulo: string; aoClicar: () => void }; variante?: 'vazio' | 'sem-resultado' | 'em-construcao' })`; `Carregando({ variante?: 'lista' | 'card' | 'kpi' | 'detalhe'; linhas?: number })`; `FeedbackInline({ tom: 'erro' | 'aviso' | 'sucesso' | 'info'; children; acao?: { rotulo; aoClicar }; aoFechar?: () => void })`.

- [ ] **Step 1: `EstadoVazio.tsx`**

```tsx
import type { ReactElement, ReactNode } from 'react';
import { Button, makeStyles } from '@fluentui/react-components';
import { Search24Regular, Wrench24Regular, DocumentAdd24Regular } from '@fluentui/react-icons';
import { designTokens, tokensUi } from '../../tokens/tokens';
import { useTipografia } from '../../tokens/tipografia';

const useStyles = makeStyles({
  root: { display: 'flex', flexDirection: 'column', alignItems: 'center', textAlign: 'center', gap: '10px', padding: `${tokensUi.espaco.xxxl} ${tokensUi.espaco.xl}` },
  icone: { width: '52px', height: '52px', borderRadius: tokensUi.raio.lg, backgroundColor: designTokens.colorNeutralLight, color: designTokens.colorNeutralMedium, display: 'grid', placeItems: 'center' },
  titulo: { marginTop: '6px' },
  descricao: { color: designTokens.colorNeutralMedium, maxWidth: '420px', margin: 0 },
  acao: { marginTop: '6px' },
});

export interface EstadoVazioProps {
  icone?: ReactElement;
  titulo: ReactNode;
  descricao?: ReactNode;
  acao?: { rotulo: string; aoClicar: () => void };
  variante?: 'vazio' | 'sem-resultado' | 'em-construcao';
}

const iconePadrao = { vazio: <DocumentAdd24Regular />, 'sem-resultado': <Search24Regular />, 'em-construcao': <Wrench24Regular /> };

// Estado vazio com orientação (spec §3): um vazio sem ação é porta fechada; com ação é convite.
// Substitui components/EstadoVazio.tsx (só uma linha cinza) e pages/EmConstrucaoPage.tsx.
export function EstadoVazio({ icone, titulo, descricao, acao, variante = 'vazio' }: EstadoVazioProps) {
  const estilos = useStyles();
  const tipo = useTipografia();
  return (
    <div className={estilos.root}>
      <div className={estilos.icone}>{icone ?? iconePadrao[variante]}</div>
      <div className={`${tipo.subtitulo} ${estilos.titulo}`}>{titulo}</div>
      {descricao && <p className={`${tipo.corpo} ${estilos.descricao}`}>{descricao}</p>}
      {acao && <Button className={estilos.acao} onClick={acao.aoClicar}>{acao.rotulo}</Button>}
    </div>
  );
}
```

- [ ] **Step 2: `Carregando.tsx`**

```tsx
import { Skeleton, SkeletonItem, makeStyles } from '@fluentui/react-components';
import { tokensUi } from '../../tokens/tokens';

const useStyles = makeStyles({
  coluna: { display: 'flex', flexDirection: 'column', gap: '10px', padding: '4px 0' },
  kpis: { display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(185px, 1fr))', gap: tokensUi.espaco.lg },
});

export interface CarregandoProps {
  variante?: 'lista' | 'card' | 'kpi' | 'detalhe';
  linhas?: number;
}

// Skeleton no formato do conteúdo que substitui (spec §3). Renomeia components/ListaCarregando.tsx e
// ganha variantes — a diferença entre "carregando" e "vazio" foi pedido do usuário em 31/08.
export function Carregando({ variante = 'lista', linhas = 4 }: CarregandoProps) {
  const estilos = useStyles();
  if (variante === 'kpi') {
    return (
      <Skeleton aria-label="Carregando indicadores" className={estilos.kpis}>
        {Array.from({ length: linhas }).map((_, i) => <SkeletonItem key={i} style={{ height: 96, borderRadius: 12 }} />)}
      </Skeleton>
    );
  }
  const altura = variante === 'card' ? 160 : variante === 'detalhe' ? 28 : 40;
  return (
    <Skeleton aria-label="Carregando" className={estilos.coluna}>
      {Array.from({ length: linhas }).map((_, i) => <SkeletonItem key={i} style={{ height: altura, borderRadius: variante === 'card' ? 12 : 6 }} />)}
    </Skeleton>
  );
}
```

- [ ] **Step 3: `FeedbackInline.tsx`**

```tsx
import type { ReactNode } from 'react';
import { Button, MessageBar, MessageBarActions, MessageBarBody, makeStyles } from '@fluentui/react-components';
import { DismissRegular } from '@fluentui/react-icons';
import { tokensUi } from '../../tokens/tokens';

const useStyles = makeStyles({ root: { marginBottom: tokensUi.espaco.lg, borderRadius: tokensUi.raio.md } });

export interface FeedbackInlineProps {
  tom: 'erro' | 'aviso' | 'sucesso' | 'info';
  children: ReactNode;
  acao?: { rotulo: string; aoClicar: () => void };
  aoFechar?: () => void;
}

const intentPorTom = { erro: 'error', aviso: 'warning', sucesso: 'success', info: 'info' } as const;

// Mensagem de feedback dentro da página (spec §3). Wrapper de MessageBar do Fluent (0 usos até aqui).
// Substitui <Text className={estilos.erro}> — erro sem ícone, sem ação e sem fechar.
export function FeedbackInline({ tom, children, acao, aoFechar }: FeedbackInlineProps) {
  const estilos = useStyles();
  return (
    <MessageBar intent={intentPorTom[tom]} className={estilos.root}>
      <MessageBarBody>{children}</MessageBarBody>
      {(acao || aoFechar) && (
        <MessageBarActions containerAction={aoFechar && <Button appearance="transparent" icon={<DismissRegular />} aria-label="Fechar" onClick={aoFechar} />}>
          {acao && <Button size="small" onClick={acao.aoClicar}>{acao.rotulo}</Button>}
        </MessageBarActions>
      )}
    </MessageBar>
  );
}
```

- [ ] **Step 4: Exportar** em `index.ts`:

```ts
export { EstadoVazio, type EstadoVazioProps } from './primitivos/EstadoVazio/EstadoVazio';
export { Carregando, type CarregandoProps } from './primitivos/Carregando/Carregando';
export { FeedbackInline, type FeedbackInlineProps } from './primitivos/FeedbackInline/FeedbackInline';
```

- [ ] **Step 5: Galeria** — adicionar:

```tsx
      <Secao titulo="EstadoVazio">
        <Card className={largura360}><EstadoVazio titulo="Nenhuma entrega registrada" descricao="Registre a primeira entrega para começar o controle de EPI desta obra." acao={{ rotulo: 'Registrar entrega', aoClicar: () => {} }} /></Card>
        <Card className={largura360}><EstadoVazio variante="sem-resultado" titulo="Nenhum resultado" descricao="Tente outro termo." acao={{ rotulo: 'Limpar busca', aoClicar: () => {} }} /></Card>
        <Card className={largura360}><EstadoVazio variante="em-construcao" titulo="Documentos e Procedimentos" descricao="Módulo reservado no menu, ainda não construído." /></Card>
      </Secao>
      <Secao titulo="Carregando">
        <Card className={largura360}><Carregando variante="lista" /></Card>
        <div style={{ width: '100%' }}><Carregando variante="kpi" /></div>
      </Secao>
      <Secao titulo="FeedbackInline">
        <div style={{ width: '100%' }}>
          <FeedbackInline tom="erro" aoFechar={() => {}}>Não foi possível salvar. Defina o responsável antes de enviar.</FeedbackInline>
          <FeedbackInline tom="info" acao={{ rotulo: 'Ver treinamento', aoClicar: () => {} }}>Campos de NR-06 preenchidos a partir do último treinamento.</FeedbackInline>
          <FeedbackInline tom="sucesso">Entrega registrada.</FeedbackInline>
        </div>
      </Secao>
```
(`largura360` vem do `makeStyles` local da galeria criado na Task 5: `{ largura360: { width: '360px' } }`.)

- [ ] **Step 6: Verificar** — `npx tsc -b && npx oxlint`; galeria mostra três vazios com ícone, skeletons e três barras de feedback.

- [ ] **Step 7: Commit**

```bash
cd C:/Projetos/SST-APP && git add src/AAHBRANT.SST.TeamsApp/src/ui && git commit -m "feat(ui): EstadoVazio, Carregando e FeedbackInline

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 7: `Abas` com sincronização de URL

**Files:**
- Create: `src/ui/compostos/Abas/Abas.tsx`
- Create: `src/ui/compostos/Abas/Abas.styles.ts`
- Modify: `src/ui/index.ts`, `src/ui/galeria/GaleriaPage.tsx`

**Interfaces:**
- Produces: `Abas<T extends string>({ nivel: 'pilar' | 'modulo' | 'interno'; abas: { valor: T; rotulo: string; contador?: number }[]; valor: T; aoMudar: (v: T) => void; param?: string; 'aria-label'?: string })`; hook `useAbaNaUrl<T extends string>(param: string, validas: readonly T[], padrao: T): [T, (v: T) => void]`.

- [ ] **Step 1: `Abas.styles.ts`** (migra `usePillTabStyles`/`useSubTabStyles` de `pageStyles.ts` para tokens novos; estilo `interno` é novo)

```ts
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
```

- [ ] **Step 2: `Abas.tsx`**

```tsx
import { useCallback } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Tab, TabList, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { useAbasStyles } from './Abas.styles';

export interface AbaItem<T extends string> { valor: T; rotulo: string; contador?: number }

export interface AbasProps<T extends string> {
  nivel: 'pilar' | 'modulo' | 'interno';
  abas: readonly AbaItem<T>[];
  valor: T;
  aoMudar: (valor: T) => void;
  'aria-label'?: string;
}

// Abas em três níveis (spec §3, §4.1). Encapsula TabList com os estilos que viviam em pageStyles
// (usePillTabStyles → pilar, useSubTabStyles → modulo) e o terceiro nível que não tinha estilo.
export function Abas<T extends string>({ nivel, abas, valor, aoMudar, 'aria-label': ariaLabel }: AbasProps<T>) {
  const estilos = useAbasStyles();
  return (
    <TabList selectedValue={valor} onTabSelect={(_: SelectTabEvent, d: SelectTabData) => aoMudar(d.value as T)} className={estilos[nivel]} aria-label={ariaLabel}>
      {abas.map((a) => (
        <Tab key={a.valor} value={a.valor}>
          {a.rotulo}
          {a.contador !== undefined && <span className={estilos.contador}>{a.contador}</span>}
        </Tab>
      ))}
    </TabList>
  );
}

// Aba sincronizada com a URL nos dois sentidos (spec §3): hoje as páginas-pilar só leem ?secao= na
// montagem — voltar do navegador e F5 perdem a aba. Este hook lê e escreve o parâmetro; a URL é a
// fonte da verdade. Usa replace para não poluir o histórico a cada clique.
export function useAbaNaUrl<T extends string>(param: string, validas: readonly T[], padrao: T): [T, (v: T) => void] {
  const [params, setParams] = useSearchParams();
  const bruto = params.get(param);
  const atual = (validas as readonly string[]).includes(bruto ?? '') ? (bruto as T) : padrao;
  const definir = useCallback((v: T) => {
    setParams((p) => { const novo = new URLSearchParams(p); novo.set(param, v); return novo; }, { replace: true });
  }, [param, setParams]);
  return [atual, definir];
}
```

- [ ] **Step 3: Exportar** em `index.ts`:

```ts
export { Abas, useAbaNaUrl, type AbasProps, type AbaItem } from './compostos/Abas/Abas';
```

- [ ] **Step 4: Galeria** — adicionar (a galeria usa `useAbaNaUrl` para demonstrar a sincronização):

```tsx
      <Secao titulo="Abas (a de módulo sincroniza com ?demo= na URL — troque e aperte F5)">
        <div style={{ width: '100%' }}>
          <Abas nivel="pilar" abas={[{ valor: 'a', rotulo: 'PGR e GRO' }, { valor: 'b', rotulo: 'PCMSO' }, { valor: 'c', rotulo: 'Treinamentos', contador: 14 }]} valor={abaPilar} aoMudar={setAbaPilar} />
          <Abas nivel="modulo" abas={[{ valor: 'x', rotulo: 'Entregas' }, { valor: 'y', rotulo: 'Estoque' }, { valor: 'z', rotulo: 'Catálogo' }]} valor={abaDemo} aoMudar={setAbaDemo} />
          <Abas nivel="interno" abas={[{ valor: 'p', rotulo: 'Por obra' }, { valor: 'q', rotulo: 'Por função' }]} valor={abaInterna} aoMudar={setAbaInterna} />
        </div>
      </Secao>
```
No topo do componente `GaleriaPage`: `const [abaPilar, setAbaPilar] = useState<'a'|'b'|'c'>('a');`, `const [abaInterna, setAbaInterna] = useState<'p'|'q'>('p');`, `const [abaDemo, setAbaDemo] = useAbaNaUrl('demo', ['x', 'y', 'z'] as const, 'x');`.

- [ ] **Step 5: Verificar** — galeria: clicar em "Estoque" muda a URL para `…?demo=y`; F5 mantém; botão voltar do navegador **não** volta a aba (replace). Aba de módulo selecionada é verde sólido; aba de pilar selecionada tem traço vinho.

- [ ] **Step 6: Commit**

```bash
cd C:/Projetos/SST-APP && git add src/AAHBRANT.SST.TeamsApp/src/ui && git commit -m "feat(ui): Abas em três níveis com sincronização de URL

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 8: `DataTable`

**Files:**
- Create: `src/ui/compostos/DataTable/DataTable.tsx`
- Create: `src/ui/compostos/DataTable/DataTable.styles.ts`
- Modify: `src/ui/index.ts`, `src/ui/galeria/GaleriaPage.tsx`

**Interfaces:**
- Produces:
```ts
interface Coluna<T> { chave: string; rotulo: ReactNode; largura?: string; alinhar?: 'esquerda' | 'direita' | 'centro'; render?: (linha: T) => ReactNode }
interface DataTableProps<T> {
  colunas: Coluna<T>[]; linhas: T[]; chaveLinha: (l: T) => string;
  carregando?: boolean; vazio?: EstadoVazioProps; densidade?: 'confortavel' | 'compacta';
  aoClicarLinha?: (l: T) => void; acoesLinha?: (l: T) => ReactNode;
  expansivel?: { aberta: (l: T) => boolean; render: (l: T) => ReactNode };
  cabecalhoFixo?: boolean; 'aria-label'?: string;
}
```
Sem `render`, a célula mostra `String((linha as Record<string, unknown>)[chave] ?? '')`.

- [ ] **Step 1: `DataTable.styles.ts`** — absorve o hack `.fui-TableRow.fui-TableRow` de `index.css` como estilo **interno**:

```ts
import { makeStyles } from '@fluentui/react-components';
import { designTokens, tokensUi } from '../../tokens/tokens';

export const useDataTableStyles = makeStyles({
  wrap: { overflowX: 'auto' },
  tabela: { width: '100%', borderCollapse: 'separate', borderSpacing: '0 8px', marginTop: '-8px' },
  compacta: { borderSpacing: '0 6px', marginTop: '-6px' },
  cabecalhoFixo: { '& thead th': { position: 'sticky', top: 0, backgroundColor: designTokens.colorPageBackground, zIndex: 1 } },
  th: { textAlign: 'left', fontSize: '12px', lineHeight: '16px', fontWeight: 600, color: designTokens.colorNeutralMedium, padding: '6px 14px 10px', borderBottom: `1px solid ${designTokens.colorCardBorder}`, whiteSpace: 'nowrap' },
  // Linha-como-cartão (pedido do usuário 03/09): vivia como sobrescrita global .fui-TableRow.fui-TableRow
  // em index.css; passa a ser estilo interno desta peça e o hack sai na Onda 3.
  tr: { boxShadow: tokensUi.sombraLinha, transitionProperty: 'transform, box-shadow', transitionDuration: '120ms', transitionTimingFunction: 'cubic-bezier(0.2, 0, 0, 1)' },
  trClicavel: { cursor: 'pointer', ':hover': { transform: 'translateY(-1px)', boxShadow: tokensUi.sombraLinhaHover } },
  td: { backgroundColor: designTokens.colorSurface, padding: '12px 14px', borderTop: `1px solid ${tokensUi.bordaSuave}`, borderBottom: `1px solid ${tokensUi.bordaSuave}`, verticalAlign: 'middle' },
  tdCompacta: { padding: '8px 12px' },
  tdPrimeira: { borderLeft: `1px solid ${tokensUi.bordaSuave}`, borderRadius: `${tokensUi.raio.md} 0 0 ${tokensUi.raio.md}` },
  tdUltima: { borderRight: `1px solid ${tokensUi.bordaSuave}`, borderRadius: `0 ${tokensUi.raio.md} ${tokensUi.raio.md} 0` },
  direita: { textAlign: 'right' },
  centro: { textAlign: 'center' },
  acoes: { display: 'inline-flex', gap: '4px', justifyContent: 'flex-end' },
  expandida: { backgroundColor: designTokens.colorNeutralLight, borderRadius: `0 0 ${tokensUi.raio.md} ${tokensUi.raio.md}`, padding: `${tokensUi.espaco.md} ${tokensUi.espaco.lg}` },
});
```

- [ ] **Step 2: `DataTable.tsx`** — usa `<table>` nativo (semântica e controle de raio por célula; o Fluent `Table noNativeElements` não permite `border-spacing`):

```tsx
import { Fragment, type ReactNode } from 'react';
import { mergeClasses } from '@fluentui/react-components';
import { EstadoVazio, type EstadoVazioProps } from '../../primitivos/EstadoVazio/EstadoVazio';
import { Carregando } from '../../primitivos/Carregando/Carregando';
import { useDataTableStyles } from './DataTable.styles';

export interface Coluna<T> {
  chave: string;
  rotulo: ReactNode;
  largura?: string;
  alinhar?: 'esquerda' | 'direita' | 'centro';
  render?: (linha: T) => ReactNode;
}

export interface DataTableProps<T> {
  colunas: Coluna<T>[];
  linhas: T[];
  chaveLinha: (linha: T) => string;
  carregando?: boolean;
  vazio?: EstadoVazioProps;
  densidade?: 'confortavel' | 'compacta';
  aoClicarLinha?: (linha: T) => void;
  acoesLinha?: (linha: T) => ReactNode;
  expansivel?: { aberta: (linha: T) => boolean; render: (linha: T) => ReactNode };
  cabecalhoFixo?: boolean;
  'aria-label'?: string;
}

// Tabela do sistema (spec §3): linhas-como-cartão, estados de carregando/vazio embutidos, densidade,
// ações por linha, linha expansível. Substitui os 63 usos de <Table> cru do Fluent.
export function DataTable<T>({ colunas, linhas, chaveLinha, carregando, vazio, densidade = 'confortavel', aoClicarLinha, acoesLinha, expansivel, cabecalhoFixo, 'aria-label': ariaLabel }: DataTableProps<T>) {
  const e = useDataTableStyles();
  const compacta = densidade === 'compacta';
  const totalColunas = colunas.length + (acoesLinha ? 1 : 0);

  if (carregando) return <Carregando variante="lista" linhas={5} />;
  if (linhas.length === 0) return <EstadoVazio titulo="Nada por aqui" {...vazio} />;

  function celula(linha: T, col: Coluna<T>) {
    if (col.render) return col.render(linha);
    const v = (linha as Record<string, unknown>)[col.chave];
    return v === null || v === undefined ? '' : String(v);
  }

  return (
    <div className={e.wrap}>
      <table className={mergeClasses(e.tabela, compacta && e.compacta, cabecalhoFixo && e.cabecalhoFixo)} aria-label={ariaLabel}>
        <thead>
          <tr>
            {colunas.map((c) => <th key={c.chave} className={mergeClasses(e.th, c.alinhar === 'direita' && e.direita, c.alinhar === 'centro' && e.centro)} style={c.largura ? { width: c.largura } : undefined}>{c.rotulo}</th>)}
            {acoesLinha && <th className={e.th} aria-label="Ações" />}
          </tr>
        </thead>
        <tbody>
          {linhas.map((linha) => {
            const chave = chaveLinha(linha);
            const aberta = expansivel?.aberta(linha) ?? false;
            const clicavel = !!aoClicarLinha;
            return (
              <Fragment key={chave}>
                <tr className={mergeClasses(e.tr, clicavel && e.trClicavel)} onClick={clicavel ? () => aoClicarLinha(linha) : undefined} aria-expanded={expansivel ? aberta : undefined}>
                  {colunas.map((c, i) => (
                    <td key={c.chave} className={mergeClasses(e.td, compacta && e.tdCompacta, i === 0 && e.tdPrimeira, i === colunas.length - 1 && !acoesLinha && e.tdUltima, c.alinhar === 'direita' && e.direita, c.alinhar === 'centro' && e.centro)}>
                      {celula(linha, c)}
                    </td>
                  ))}
                  {acoesLinha && (
                    <td className={mergeClasses(e.td, compacta && e.tdCompacta, e.tdUltima, e.direita)} onClick={(ev) => ev.stopPropagation()}>
                      <div className={e.acoes}>{acoesLinha(linha)}</div>
                    </td>
                  )}
                </tr>
                {expansivel && aberta && (
                  <tr>
                    <td colSpan={totalColunas} style={{ padding: 0, border: 0 }}>
                      <div className={e.expandida}>{expansivel.render(linha)}</div>
                    </td>
                  </tr>
                )}
              </Fragment>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
```

- [ ] **Step 3: Exportar** em `index.ts`:

```ts
export { DataTable, type DataTableProps, type Coluna } from './compostos/DataTable/DataTable';
```

- [ ] **Step 4: Galeria** — adicionar, com dados fixos:

```tsx
      <Secao titulo="DataTable">
        <div style={{ width: '100%' }}>
          <Card densidade="compacta">
            <DataTable
              aria-label="Entregas de exemplo"
              colunas={[
                { chave: 'nome', rotulo: 'Funcionário' },
                { chave: 'epi', rotulo: 'EPI' },
                { chave: 'validade', rotulo: 'Validade', render: (l) => { const n = nivelVencimento(l.validade); return n ? <StatusChip tom={tomDeVencimento(n)}>{rotuloDeVencimento(n)}</StatusChip> : '—'; } },
              ]}
              linhas={[
                { id: '1', nome: 'João da Silva', epi: 'Capacete classe B', validade: '2099-03-12' },
                { id: '2', nome: 'Ana Carolina Reis', epi: 'Luva isolante', validade: new Date(Date.now() + 8 * 86_400_000).toISOString().slice(0, 10) },
                { id: '3', nome: 'Roberto Pereira', epi: 'Cinto paraquedista', validade: '2020-02-02' },
              ]}
              chaveLinha={(l) => l.id}
              acoesLinha={() => <Button size="small" appearance="subtle">Assinar</Button>}
              expansivel={{ aberta: (l) => l.id === abertaDemo, render: (l) => <span>Detalhe de {l.nome}</span> }}
              aoClicarLinha={(l) => setAbertaDemo((a) => (a === l.id ? null : l.id))}
            />
          </Card>
          <Card densidade="compacta" className={margemTopo}><DataTable colunas={[{ chave: 'a', rotulo: 'A' }]} linhas={[]} chaveLinha={() => ''} vazio={{ titulo: 'Nenhuma entrega', descricao: 'Registre a primeira.', acao: { rotulo: 'Registrar', aoClicar: () => {} } }} /></Card>
          <Card densidade="compacta" className={margemTopo}><DataTable colunas={[{ chave: 'a', rotulo: 'A' }]} linhas={[]} chaveLinha={() => ''} carregando /></Card>
        </div>
      </Secao>
```
No topo: `const [abertaDemo, setAbertaDemo] = useState<string | null>(null);`. `margemTopo` no `makeStyles` local: `{ margemTopo: { marginTop: '16px' } }`.

- [ ] **Step 5: Verificar** — linhas como cartões com sombra, hover levanta 1px, clique expande, chips de vencimento nos três tons, vazio com ação, skeleton.

- [ ] **Step 6: Commit**

```bash
cd C:/Projetos/SST-APP && git add src/AAHBRANT.SST.TeamsApp/src/ui && git commit -m "feat(ui): DataTable com linhas-cartão, vazio, carregando e expansão

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 9: `FormSection`, `FormGrid`, `Campo`, `ChipCheckboxGroup`

**Files:**
- Create: `src/ui/compostos/Formulario/FormSection.tsx`
- Create: `src/ui/compostos/Formulario/FormGrid.tsx`
- Create: `src/ui/primitivos/ChipCheckboxGroup/ChipCheckboxGroup.tsx`
- Modify: `src/ui/index.ts`, `src/ui/galeria/GaleriaPage.tsx`

**Interfaces:**
- Produces: `FormSection({ titulo: ReactNode; numero?: number; primeira?: boolean; children })`; `FormGrid({ children })`; `Campo({ span?: 2|3|4|5|6|12; children })`; `FormRodape({ info?: ReactNode; children })`; `ChipCheckboxGroup({ opcoes: { id: string; rotulo: string }[]; selecionados: string[]; aoMudar: (ids: string[]) => void; 'aria-label'?: string })`.

- [ ] **Step 1: `FormSection.tsx`**

```tsx
import type { ReactNode } from 'react';
import { makeStyles, mergeClasses } from '@fluentui/react-components';
import { designTokens, tokensUi } from '../../tokens/tokens';
import { useTipografia } from '../../tokens/tipografia';

const useStyles = makeStyles({
  titulo: { color: designTokens.colorNeutralMedium, marginTop: tokensUi.espaco.xl, marginBottom: '14px', paddingBottom: tokensUi.espaco.sm, borderBottom: `1px solid ${tokensUi.bordaSuave}` },
  primeira: { marginTop: 0 },
  rodape: { marginTop: tokensUi.espaco.xl, paddingTop: tokensUi.espaco.lg, borderTop: `1px solid ${tokensUi.bordaSuave}`, display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: tokensUi.espaco.lg, flexWrap: 'wrap' },
  info: { color: designTokens.colorNeutralMedium, maxWidth: '700px' },
});

// Seção de formulário (spec §3): rótulo pequeno em versalete que divide um formulário longo em blocos
// nomeados. Formaliza usePageStyles.sectionTitle/sectionTitleFirst.
export function FormSection({ titulo, numero, primeira, children }: { titulo: ReactNode; numero?: number; primeira?: boolean; children: ReactNode }) {
  const e = useStyles(); const tipo = useTipografia();
  return (
    <>
      <div className={mergeClasses(tipo.micro, e.titulo, primeira && e.primeira)}>{numero !== undefined ? `${numero}. ` : ''}{titulo}</div>
      {children}
    </>
  );
}

// Rodapé de formulário longo: texto de ajuda à esquerda, ações à direita. Formaliza usePageStyles.footer.
export function FormRodape({ info, children }: { info?: ReactNode; children: ReactNode }) {
  const e = useStyles(); const tipo = useTipografia();
  return (
    <div className={e.rodape}>
      {info ? <div className={mergeClasses(tipo.legenda, e.info)}>{info}</div> : <span />}
      <div style={{ display: 'flex', gap: 8 }}>{children}</div>
    </div>
  );
}
```

- [ ] **Step 2: `FormGrid.tsx`**

```tsx
import type { ReactNode } from 'react';
import { makeStyles, mergeClasses } from '@fluentui/react-components';
import { tokensUi } from '../../tokens/tokens';

const useStyles = makeStyles({
  grid: { display: 'grid', gridTemplateColumns: 'repeat(12, 1fr)', gap: tokensUi.espaco.lg, marginBottom: '20px',
    // Único breakpoint do app hoje (pageStyles.formGrid) — fica aqui dentro.
    '@media (max-width: 900px)': { gridTemplateColumns: 'repeat(1, 1fr)' } },
  s2: { gridColumn: 'span 2' }, s3: { gridColumn: 'span 3' }, s4: { gridColumn: 'span 4' }, s5: { gridColumn: 'span 5' }, s6: { gridColumn: 'span 6' }, s12: { gridColumn: 'span 12' },
  '@media (max-width: 900px)': {},
});

// Grade de 12 colunas para formulários com larguras deliberadas (spec §3). Formaliza
// usePageStyles.formGrid + col2..col12. Abaixo de 900px tudo vira uma coluna.
export function FormGrid({ children }: { children: ReactNode }) {
  const e = useStyles();
  return <div className={e.grid}>{children}</div>;
}

export function Campo({ span = 12, children }: { span?: 2 | 3 | 4 | 5 | 6 | 12; children: ReactNode }) {
  const e = useStyles();
  const classe = { 2: e.s2, 3: e.s3, 4: e.s4, 5: e.s5, 6: e.s6, 12: e.s12 }[span];
  return <div className={mergeClasses(classe)}>{children}</div>;
}
```
(Griffel não aceita `@media` como chave de topo vazia — remova a linha `'@media (max-width: 900px)': {},` se o TypeScript reclamar; ela está aí só como lembrete de que o breakpoint vive em `grid`.)

- [ ] **Step 3: `ChipCheckboxGroup.tsx`** (formaliza `useCheckboxChipStyles`)

```tsx
import { Checkbox, makeStyles, shorthands, tokens } from '@fluentui/react-components';
import { designTokens, tokensUi } from '../../tokens/tokens';

const useStyles = makeStyles({
  grupo: { display: 'flex', flexWrap: 'wrap', gap: tokensUi.espaco.sm },
  chip: {
    display: 'inline-flex', alignItems: 'center', backgroundColor: designTokens.colorNeutralLight,
    ...shorthands.borderRadius(tokensUi.raio.full), ...shorthands.border('1px', 'solid', designTokens.colorCardBorder),
    ...shorthands.padding('6px', '14px', '6px', '10px'), cursor: 'pointer',
    transitionProperty: 'background-color, border-color', transitionDuration: '120ms',
    ':hover': { backgroundColor: tokens.colorNeutralBackground1Hover, ...shorthands.borderColor(tokensUi.chrome.ativoFundo) },
  },
});

export interface ChipCheckboxGroupProps {
  opcoes: { id: string; rotulo: string }[];
  selecionados: string[];
  aoMudar: (ids: string[]) => void;
  'aria-label'?: string;
}

// Seleção múltipla em chips clicáveis inteiros (pedido do usuário 02/09; spec §3). Formaliza
// useCheckboxChipStyles, usado em matrizes, PT e DDS.
export function ChipCheckboxGroup({ opcoes, selecionados, aoMudar, 'aria-label': ariaLabel }: ChipCheckboxGroupProps) {
  const e = useStyles();
  function alternar(id: string, marcado: boolean) {
    aoMudar(marcado ? [...selecionados, id] : selecionados.filter((s) => s !== id));
  }
  return (
    <div role="group" aria-label={ariaLabel} className={e.grupo}>
      {opcoes.map((o) => (
        <Checkbox key={o.id} className={e.chip} label={o.rotulo} checked={selecionados.includes(o.id)} onChange={(_, d) => alternar(o.id, !!d.checked)} />
      ))}
    </div>
  );
}
```

- [ ] **Step 4: Exportar** em `index.ts`:

```ts
export { FormSection, FormRodape } from './compostos/Formulario/FormSection';
export { FormGrid, Campo } from './compostos/Formulario/FormGrid';
export { ChipCheckboxGroup, type ChipCheckboxGroupProps } from './primitivos/ChipCheckboxGroup/ChipCheckboxGroup';
```

- [ ] **Step 5: Galeria** — adicionar:

```tsx
      <Secao titulo="FormSection + FormGrid + FormRodape">
        <Card className={larguraTotal}>
          <FormSection titulo="Dados da entrega" numero={1} primeira>
            <FormGrid>
              <Campo span={6}><Field label="Funcionário"><Input /></Field></Campo>
              <Campo span={3}><Field label="Quantidade"><Input type="number" defaultValue="1" /></Field></Campo>
              <Campo span={3}><Field label="Entrega"><CampoData value="2026-09-07" onChange={() => {}} /></Field></Campo>
            </FormGrid>
          </FormSection>
          <FormSection titulo="Observações" numero={2}>
            <FormGrid><Campo><Field label="Texto"><Textarea /></Field></Campo></FormGrid>
          </FormSection>
          <FormRodape info="Ações concluídas exigem evidência fotográfica antes do encerramento."><Button>Cancelar</Button><Button appearance="primary">Salvar</Button></FormRodape>
        </Card>
      </Secao>
      <Secao titulo="ChipCheckboxGroup">
        <ChipCheckboxGroup aria-label="EPIs" opcoes={[{ id: 'a', rotulo: 'Capacete' }, { id: 'b', rotulo: 'Botina' }, { id: 'c', rotulo: 'Luva' }, { id: 'd', rotulo: 'Óculos' }]} selecionados={chipsDemo} aoMudar={setChipsDemo} />
      </Secao>
```
No topo: `const [chipsDemo, setChipsDemo] = useState<string[]>(['a']);`. `larguraTotal: { width: '100%' }` no `makeStyles` local.

- [ ] **Step 6: Verificar** — grade 12 colunas; ao estreitar a janela abaixo de 900px, empilha; chips inteiros clicáveis.

- [ ] **Step 7: Commit**

```bash
cd C:/Projetos/SST-APP && git add src/AAHBRANT.SST.TeamsApp/src/ui && git commit -m "feat(ui): FormSection, FormGrid, Campo, FormRodape e ChipCheckboxGroup

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 10: `SeletorPesquisavel` + `ConfirmDialog`/`useConfirmar`

**Files:**
- Create: `src/ui/primitivos/SeletorPesquisavel/SeletorPesquisavel.tsx`
- Create: `src/ui/primitivos/ConfirmDialog/useConfirmar.tsx`
- Modify: `src/ui/index.ts`, `src/ui/galeria/GaleriaPage.tsx`

**Interfaces:**
- Produces: `SeletorPesquisavel({ opcoes: { id: string; rotulo: string; descricao?: string }[]; valor: string; aoMudar: (id: string) => void; placeholder?: string; vazio?: string; disabled?: boolean; 'aria-label'?: string })`; `useConfirmar(): { confirmar: (opcoes: string | { titulo?: string; mensagem: string; rotuloConfirmar?: string; tom?: 'destrutivo' | 'neutro' }) => Promise<boolean>; dialogElement: ReactElement }`.

- [ ] **Step 1: `SeletorPesquisavel.tsx`**

```tsx
import { useMemo, useState } from 'react';
import { Combobox, Option, makeStyles } from '@fluentui/react-components';
import { designTokens } from '../../tokens/tokens';

const useStyles = makeStyles({
  root: { width: '100%', minWidth: 0 },
  descricao: { display: 'block', color: designTokens.colorNeutralMedium, fontSize: '11px', lineHeight: '14px', fontWeight: 600 },
});

export interface OpcaoSeletor { id: string; rotulo: string; descricao?: string }

export interface SeletorPesquisavelProps {
  opcoes: OpcaoSeletor[];
  valor: string;
  aoMudar: (id: string) => void;
  placeholder?: string;
  vazio?: string;
  disabled?: boolean;
  'aria-label'?: string;
}

function normalizar(s: string) {
  return s.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase();
}

// Seletor com busca por texto (spec §3). Wrapper de Combobox do Fluent (0 usos até aqui). Substitui
// <Select> quando a lista passa de ~15 itens — trabalhadores, EPIs, cursos, obras. Ignora acentos.
export function SeletorPesquisavel({ opcoes, valor, aoMudar, placeholder, vazio = 'Nenhum resultado', disabled, 'aria-label': ariaLabel }: SeletorPesquisavelProps) {
  const e = useStyles();
  const [busca, setBusca] = useState('');
  const selecionada = opcoes.find((o) => o.id === valor);
  const filtradas = useMemo(() => {
    const q = normalizar(busca.trim());
    if (!q) return opcoes;
    return opcoes.filter((o) => normalizar(`${o.rotulo} ${o.descricao ?? ''}`).includes(q));
  }, [busca, opcoes]);

  return (
    <Combobox
      className={e.root}
      aria-label={ariaLabel}
      placeholder={placeholder}
      disabled={disabled}
      freeform
      value={busca || selecionada?.rotulo || ''}
      selectedOptions={valor ? [valor] : []}
      onChange={(ev) => setBusca(ev.target.value)}
      onOptionSelect={(_, d) => { aoMudar(d.optionValue ?? ''); setBusca(''); }}
      onOpenChange={(_, d) => { if (!d.open) setBusca(''); }}
    >
      {filtradas.length === 0 && <Option disabled value="__vazio" text={vazio}>{vazio}</Option>}
      {filtradas.map((o) => (
        <Option key={o.id} value={o.id} text={o.rotulo}>
          <span>{o.rotulo}{o.descricao && <span className={e.descricao}>{o.descricao}</span>}</span>
        </Option>
      ))}
    </Combobox>
  );
}
```

- [ ] **Step 2: `useConfirmar.tsx`** (absorve `hooks/useConfirmarExclusao.tsx`; API compatível: `await confirmar('mensagem')`)

```tsx
import { useCallback, useRef, useState } from 'react';
import { Button, Dialog, DialogActions, DialogBody, DialogContent, DialogSurface, DialogTitle, makeStyles } from '@fluentui/react-components';
import { tokensUi } from '../../tokens/tokens';

const useStyles = makeStyles({
  // Destrutivo é ação, não estado — única exceção declarada à regra "status nunca sólido" (spec §3).
  destrutivo: { backgroundColor: tokensUi.status.alerta.tinta, color: '#ffffff', ':hover': { backgroundColor: tokensUi.status.alerta.tinta, color: '#ffffff', filter: 'brightness(0.92)' } },
});

export interface OpcoesConfirmacao {
  titulo?: string;
  mensagem: string;
  rotuloConfirmar?: string;
  tom?: 'destrutivo' | 'neutro';
}

// Confirmação por promise (spec §3). Absorve useConfirmarExclusao (39 telas, pedido de 31/08) mantendo
// a mesma API — e corrige o botão Excluir, que usava appearance="primary" (vinho, cara de ação
// principal) para uma ação destrutiva.
export function useConfirmar() {
  const e = useStyles();
  const [aberto, setAberto] = useState(false);
  const [opcoes, setOpcoes] = useState<OpcoesConfirmacao>({ mensagem: '' });
  const resolverRef = useRef<((v: boolean) => void) | null>(null);

  const confirmar = useCallback((entrada: OpcoesConfirmacao | string) => {
    setOpcoes(typeof entrada === 'string' ? { mensagem: entrada, tom: 'destrutivo' } : { tom: 'destrutivo', ...entrada });
    setAberto(true);
    return new Promise<boolean>((resolve) => { resolverRef.current = resolve; });
  }, []);

  function responder(v: boolean) { setAberto(false); resolverRef.current?.(v); resolverRef.current = null; }

  const destrutivo = (opcoes.tom ?? 'destrutivo') === 'destrutivo';
  const dialogElement = (
    <Dialog open={aberto} onOpenChange={(_, d) => { if (!d.open) responder(false); }}>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>{opcoes.titulo ?? (destrutivo ? 'Confirmar exclusão' : 'Confirmar')}</DialogTitle>
          <DialogContent>{opcoes.mensagem}</DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={() => responder(false)}>Cancelar</Button>
            <Button appearance={destrutivo ? 'secondary' : 'primary'} className={destrutivo ? e.destrutivo : undefined} onClick={() => responder(true)}>
              {opcoes.rotuloConfirmar ?? (destrutivo ? 'Excluir' : 'Confirmar')}
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );

  return { confirmar, dialogElement };
}
```

- [ ] **Step 3: Exportar** em `index.ts`:

```ts
export { SeletorPesquisavel, type SeletorPesquisavelProps, type OpcaoSeletor } from './primitivos/SeletorPesquisavel/SeletorPesquisavel';
export { useConfirmar, type OpcoesConfirmacao } from './primitivos/ConfirmDialog/useConfirmar';
```

- [ ] **Step 4: Galeria** — adicionar:

```tsx
      <Secao titulo="SeletorPesquisavel (digite 'jo')">
        <div className={largura360}>
          <Field label="Funcionário">
            <SeletorPesquisavel aria-label="Funcionário" placeholder="Buscar entre 312 funcionários" valor={funcDemo} aoMudar={setFuncDemo}
              opcoes={[{ id: '1', rotulo: 'João da Silva', descricao: 'Pedreiro, Ponte Rio Cuiá' }, { id: '2', rotulo: 'Joana Martins', descricao: 'Carpinteira, Vila Nova' }, { id: '3', rotulo: 'José Almeida', descricao: 'Operador de guindaste' }, { id: '4', rotulo: 'Ana Carolina Reis', descricao: 'Eletricista' }]} />
          </Field>
        </div>
      </Secao>
      <Secao titulo="ConfirmDialog">
        <Button onClick={async () => { const ok = await confirmar('Excluir a entrega de João da Silva? Esta ação não pode ser desfeita.'); setUltimaConfirmacao(ok ? 'confirmou' : 'cancelou'); }}>Abrir destrutivo</Button>
        <Button onClick={async () => { const ok = await confirmar({ titulo: 'Enviar ao responsável', mensagem: 'A ocorrência será enviada a Carlos Mendes.', rotuloConfirmar: 'Enviar', tom: 'neutro' }); setUltimaConfirmacao(ok ? 'confirmou' : 'cancelou'); }}>Abrir neutro</Button>
        <span className={tipo.legenda}>Última resposta: {ultimaConfirmacao ?? '—'}</span>
        {dialogElement}
      </Secao>
```
No topo: `const [funcDemo, setFuncDemo] = useState('');`, `const { confirmar, dialogElement } = useConfirmar();`, `const [ultimaConfirmacao, setUltimaConfirmacao] = useState<string | null>(null);`.

- [ ] **Step 5: Verificar** — digitar "jo" filtra para 3 nomes ignorando acento; escolher preenche; o diálogo destrutivo tem botão vermelho, o neutro tem botão vinho.

- [ ] **Step 6: Commit**

```bash
cd C:/Projetos/SST-APP && git add src/AAHBRANT.SST.TeamsApp/src/ui && git commit -m "feat(ui): SeletorPesquisavel e useConfirmar com tom destrutivo

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 11: `PainelLateral` + `KpiCard`

**Files:**
- Create: `src/ui/compostos/PainelLateral/PainelLateral.tsx`
- Create: `src/ui/compostos/KpiCard/KpiCard.tsx`
- Modify: `src/ui/index.ts`, `src/ui/galeria/GaleriaPage.tsx`

**Interfaces:**
- Produces: `PainelLateral({ aberto: boolean; aoFechar: () => void; titulo: ReactNode; subtitulo?: ReactNode; largura?: 'md' | 'lg'; rodape?: ReactNode; children })`; `KpiCard({ rotulo: ReactNode; valor: ReactNode; tom: Tom; icone?: ReactElement; deltas?: { texto: string; tom: Tom }[]; indice?: number; carregando?: boolean })`.

- [ ] **Step 1: `PainelLateral.tsx`** (wrapper de `OverlayDrawer`; absorve o estilo de `TrabalhadoresGaveta`)

```tsx
import type { ReactNode } from 'react';
import { Button, DrawerBody, DrawerFooter, DrawerHeader, DrawerHeaderTitle, OverlayDrawer, makeStyles, mergeClasses } from '@fluentui/react-components';
import { Dismiss24Regular } from '@fluentui/react-icons';
import { designTokens, tokensUi } from '../../tokens/tokens';
import { useTipografia } from '../../tokens/tipografia';

const useStyles = makeStyles({
  md: { width: '400px', maxWidth: '94vw' },
  lg: { width: '560px', maxWidth: '94vw' },
  cabecalho: { borderBottom: `1px solid ${tokensUi.bordaSuave}` },
  subtitulo: { color: designTokens.colorNeutralMedium, marginTop: tokensUi.espaco.xs },
  rodape: { borderTop: `1px solid ${tokensUi.bordaSuave}`, justifyContent: 'flex-end', gap: tokensUi.espaco.sm },
});

export interface PainelLateralProps {
  aberto: boolean;
  aoFechar: () => void;
  titulo: ReactNode;
  subtitulo?: ReactNode;
  largura?: 'md' | 'lg';
  rodape?: ReactNode;
  children: ReactNode;
}

// Painel que desliza da borda direita (spec §3, §4.2): formulários de criação saem de cima da tabela
// e vêm para aqui, com a lista visível atrás. Wrapper de OverlayDrawer (o Fluent já anima com a
// curva certa); absorve o estilo de pages/pessoas/TrabalhadoresGaveta.tsx.
export function PainelLateral({ aberto, aoFechar, titulo, subtitulo, largura = 'md', rodape, children }: PainelLateralProps) {
  const e = useStyles(); const tipo = useTipografia();
  return (
    <OverlayDrawer open={aberto} position="end" onOpenChange={(_, d) => { if (!d.open) aoFechar(); }} className={e[largura]}>
      <DrawerHeader className={e.cabecalho}>
        <DrawerHeaderTitle action={<Button appearance="subtle" aria-label="Fechar" icon={<Dismiss24Regular />} onClick={aoFechar} />}>
          <span className={tipo.subtitulo}>{titulo}</span>
          {subtitulo && <div className={mergeClasses(tipo.legenda, e.subtitulo)}>{subtitulo}</div>}
        </DrawerHeaderTitle>
      </DrawerHeader>
      <DrawerBody>{children}</DrawerBody>
      {rodape && <DrawerFooter className={e.rodape}>{rodape}</DrawerFooter>}
    </OverlayDrawer>
  );
}
```

- [ ] **Step 2: `KpiCard.tsx`** (promove `components/dashboard/KpiCard.tsx`; `tom` no lugar de `cor: string`)

```tsx
import type { ReactElement, ReactNode } from 'react';
import { motion } from 'framer-motion';
import { makeStyles, mergeClasses } from '@fluentui/react-components';
import { designTokens, tokensUi, type Tom } from '../../tokens/tokens';
import { useTipografia } from '../../tokens/tipografia';
import { escalonado } from '../../tokens/movimento';
import { Carregando } from '../../primitivos/Carregando/Carregando';

const useStyles = makeStyles({
  root: { backgroundColor: designTokens.colorSurface, border: `1px solid ${designTokens.colorCardBorder}`, borderRadius: tokensUi.raio.lg, boxShadow: designTokens.cardShadow, padding: tokensUi.espaco.xl, display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: '10px' },
  textos: { display: 'flex', flexDirection: 'column', gap: '6px', minWidth: 0 },
  rotulo: { color: designTokens.colorNeutralMedium },
  icone: { width: '42px', height: '42px', borderRadius: '50%', display: 'grid', placeItems: 'center', flexShrink: 0 },
  deltas: { display: 'flex', flexWrap: 'wrap', gap: '6px' },
  delta: { fontSize: '11px', lineHeight: '14px', fontWeight: 700, padding: '3px 8px', borderRadius: tokensUi.raio.full },
  ok: { color: tokensUi.status.ok.tinta, backgroundColor: tokensUi.status.ok.fundo },
  atencao: { color: tokensUi.status.atencao.tinta, backgroundColor: tokensUi.status.atencao.fundo },
  alerta: { color: tokensUi.status.alerta.tinta, backgroundColor: tokensUi.status.alerta.fundo },
  info: { color: tokensUi.status.info.tinta, backgroundColor: tokensUi.status.info.fundo },
  neutro: { color: tokensUi.status.neutro.tinta, backgroundColor: tokensUi.status.neutro.fundo },
});

export interface KpiCardProps {
  rotulo: ReactNode;
  valor: ReactNode;
  tom: Tom;
  icone?: ReactElement;
  deltas?: { texto: string; tom: Tom }[];
  indice?: number;
  carregando?: boolean;
}

// Cartão de indicador (spec §3): valor grande + rótulo à esquerda, ícone em círculo lavado à direita,
// pílulas de variação. Promove components/dashboard/KpiCard.tsx trocando `cor: string` por `tom` —
// é o que elimina os 41 hex soltos dos painéis. Entrada escalonada pelo `indice`.
export function KpiCard({ rotulo, valor, tom, icone, deltas, indice = 0, carregando }: KpiCardProps) {
  const e = useStyles(); const tipo = useTipografia();
  if (carregando) return <Carregando variante="kpi" linhas={1} />;
  return (
    <motion.div className={e.root} variants={escalonado(indice)} initial="inicial" animate="visivel">
      <div className={e.textos}>
        <span className={tipo.display}>{valor}</span>
        <span className={mergeClasses(tipo.legenda, e.rotulo)}>{rotulo}</span>
        {deltas && deltas.length > 0 && (
          <div className={e.deltas}>{deltas.map((d) => <span key={d.texto} className={mergeClasses(e.delta, e[d.tom])}>{d.texto}</span>)}</div>
        )}
      </div>
      {icone && <div className={mergeClasses(e.icone, e[tom])}>{icone}</div>}
    </motion.div>
  );
}
```

- [ ] **Step 3: Exportar** em `index.ts`:

```ts
export { PainelLateral, type PainelLateralProps } from './compostos/PainelLateral/PainelLateral';
export { KpiCard, type KpiCardProps } from './compostos/KpiCard/KpiCard';
```

- [ ] **Step 4: Galeria** — adicionar:

```tsx
      <Secao titulo="PainelLateral">
        <Button appearance="primary" onClick={() => setPainelAberto(true)}>Abrir painel</Button>
        <PainelLateral aberto={painelAberto} aoFechar={() => setPainelAberto(false)} titulo="Nova entrega de EPI" subtitulo="A lista continua visível atrás." rodape={<><Button onClick={() => setPainelAberto(false)}>Cancelar</Button><Button appearance="primary" onClick={() => setPainelAberto(false)}>Registrar</Button></>}>
          <FormSection titulo="Quem recebe" numero={1} primeira><FormGrid><Campo><Field label="Funcionário"><Input /></Field></Campo></FormGrid></FormSection>
        </PainelLateral>
      </Secao>
      <Secao titulo="KpiCard (entrada escalonada — recarregue a página)">
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(185px, 1fr))', gap: 16, width: '100%' }}>
          <KpiCard indice={0} tom="info" rotulo="Obras ativas" valor="7" deltas={[{ texto: '5 em andamento', tom: 'neutro' }]} icone={<BuildingBank24Regular />} />
          <KpiCard indice={1} tom="ok" rotulo="Conformidade de EPI" valor="94%" deltas={[{ texto: '287 entregas ativas', tom: 'neutro' }]} icone={<ShieldCheckmark24Regular />} />
          <KpiCard indice={2} tom="atencao" rotulo="Treinamentos em dia" valor="87%" deltas={[{ texto: '14 a vencer', tom: 'atencao' }, { texto: '6 vencidos', tom: 'alerta' }]} icone={<DocumentCheckmark24Regular />} />
          <KpiCard indice={3} tom="alerta" rotulo="Não conformidades abertas" valor="12" deltas={[{ texto: '4 em tratamento', tom: 'alerta' }]} icone={<DocumentError24Regular />} />
          <KpiCard indice={4} tom="info" rotulo="Carregando" valor="" carregando />
        </div>
      </Secao>
```
No topo: `const [painelAberto, setPainelAberto] = useState(false);` e importar os quatro ícones de `@fluentui/react-icons`.

- [ ] **Step 5: Verificar** — painel desliza da direita, fecha no X, no overlay e em Esc; KPIs sobem escalonados 40ms ao recarregar; com "reduzir movimento" ativado no SO, aparecem instantâneos.

- [ ] **Step 6: Commit**

```bash
cd C:/Projetos/SST-APP && git add src/AAHBRANT.SST.TeamsApp/src/ui && git commit -m "feat(ui): PainelLateral e KpiCard com tom semântico

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 12: `DetailPageLayout` + `WorkflowActions`

**Files:**
- Create: `src/ui/layout/DetailPageLayout/DetailPageLayout.tsx`
- Create: `src/ui/layout/WorkflowActions/WorkflowActions.tsx`
- Modify: `src/ui/index.ts`, `src/ui/galeria/GaleriaPage.tsx`

**Interfaces:**
- Produces: `DetailPageLayout({ cabecalho: PageHeaderProps; lateral?: ReactNode; children })`; `WorkflowActions({ acoes: AcaoWorkflow[]; processando?: boolean })` com `AcaoWorkflow = { chave: string; rotulo: string; descricao?: string; tom?: 'primario' | 'neutro' | 'destrutivo'; habilitada?: boolean; formulario?: ReactNode; aoExecutar: () => void | Promise<void>; rotuloExecutar?: string }`.

- [ ] **Step 1: `DetailPageLayout.tsx`**

```tsx
import type { ReactNode } from 'react';
import { makeStyles } from '@fluentui/react-components';
import { tokensUi } from '../../tokens/tokens';
import { PageHeader, type PageHeaderProps } from '../../compostos/PageHeader/PageHeader';

const useStyles = makeStyles({
  grid: { display: 'grid', gridTemplateColumns: '1fr 320px', gap: tokensUi.espaco.lg, alignItems: 'start',
    '@media (max-width: 1100px)': { gridTemplateColumns: '1fr' } },
  lateral: { display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.lg, position: 'sticky', top: 0,
    '@media (max-width: 1100px)': { order: -1, position: 'static' } },
  principal: { display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.lg, minWidth: 0 },
});

export interface DetailPageLayoutProps {
  cabecalho: PageHeaderProps;
  lateral?: ReactNode;
  children: ReactNode;
}

// Página de detalhe (spec §3, §4.3): cabeçalho com voltar/título/status/ações; lateral fixa à direita
// com resumo e ações do fluxo; conteúdo em seções à esquerda. Abaixo de 1100px a lateral sobe.
// É a forma das 6 páginas de detalhe de ~500 linhas (NC, PCMSO, DDS, Inspeção, Reunião CIPA, PT).
export function DetailPageLayout({ cabecalho, lateral, children }: DetailPageLayoutProps) {
  const e = useStyles();
  return (
    <div>
      <PageHeader {...cabecalho} />
      <div className={e.grid}>
        <div className={e.principal}>{children}</div>
        {lateral && <aside className={e.lateral}>{lateral}</aside>}
      </div>
    </div>
  );
}
```

- [ ] **Step 2: `WorkflowActions.tsx`**

```tsx
import { useState, type ReactNode } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Button, makeStyles, mergeClasses } from '@fluentui/react-components';
import { ChevronDown16Regular } from '@fluentui/react-icons';
import { designTokens, tokensUi } from '../../tokens/tokens';
import { useTipografia } from '../../tokens/tipografia';
import { transicaoNormal } from '../../tokens/movimento';

const useStyles = makeStyles({
  lista: { display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.sm },
  acao: { border: `1px solid ${designTokens.colorCardBorder}`, borderRadius: tokensUi.raio.md, overflow: 'hidden', backgroundColor: designTokens.colorSurface },
  botao: { width: '100%', display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '10px', padding: '10px 12px', border: 0, background: 'transparent', textAlign: 'left', cursor: 'pointer', font: 'inherit', color: 'inherit', ':hover': { backgroundColor: designTokens.colorNeutralLight }, ':disabled': { opacity: 0.5, cursor: 'default' } },
  descricao: { display: 'block', color: designTokens.colorNeutralMedium, marginTop: '2px' },
  seta: { color: designTokens.colorNeutralMedium, flexShrink: 0, transitionProperty: 'transform', transitionDuration: '200ms' },
  setaAberta: { transform: 'rotate(180deg)' },
  destrutivo: { color: tokensUi.status.alerta.tinta },
  primario: { color: designTokens.colorPrimary },
  formulario: { padding: tokensUi.espaco.md, borderTop: `1px solid ${tokensUi.bordaSuave}`, backgroundColor: designTokens.colorNeutralLight, display: 'flex', flexDirection: 'column', gap: '10px' },
  botaoDestrutivo: { backgroundColor: tokensUi.status.alerta.tinta, color: '#ffffff', ':hover': { backgroundColor: tokensUi.status.alerta.tinta, color: '#ffffff', filter: 'brightness(0.92)' } },
});

export interface AcaoWorkflow {
  chave: string;
  rotulo: string;
  descricao?: string;
  tom?: 'primario' | 'neutro' | 'destrutivo';
  habilitada?: boolean;
  formulario?: ReactNode;
  aoExecutar: () => void | Promise<void>;
  rotuloExecutar?: string;
}

export interface WorkflowActionsProps { acoes: AcaoWorkflow[]; processando?: boolean }

// Ações do fluxo de um registro (spec §3, §4.3), extraídas de NaoConformidadeDetalhePage. A página
// passa só as ações permitidas no estado atual; cada uma abre o próprio formulário inline (origem: cresce
// do botão), uma por vez. Sem formulário, o clique executa direto.
export function WorkflowActions({ acoes, processando }: WorkflowActionsProps) {
  const e = useStyles(); const tipo = useTipografia();
  const [aberta, setAberta] = useState<string | null>(null);
  return (
    <div className={e.lista}>
      {acoes.map((a) => {
        const estaAberta = aberta === a.chave;
        const habilitada = a.habilitada ?? true;
        return (
          <div key={a.chave} className={e.acao}>
            <button type="button" className={mergeClasses(e.botao, a.tom === 'destrutivo' && e.destrutivo, a.tom === 'primario' && e.primario)} disabled={!habilitada || processando}
              aria-expanded={a.formulario ? estaAberta : undefined}
              onClick={() => { if (a.formulario) setAberta(estaAberta ? null : a.chave); else void a.aoExecutar(); }}>
              <span>
                <span className={tipo.corpo} style={{ fontWeight: 700 }}>{a.rotulo}</span>
                {a.descricao && <span className={mergeClasses(tipo.legenda, e.descricao)}>{a.descricao}</span>}
              </span>
              {a.formulario && <ChevronDown16Regular className={mergeClasses(e.seta, estaAberta && e.setaAberta)} />}
            </button>
            <AnimatePresence initial={false}>
              {a.formulario && estaAberta && (
                <motion.div key="f" initial={{ height: 0, opacity: 0 }} animate={{ height: 'auto', opacity: 1 }} exit={{ height: 0, opacity: 0 }} transition={transicaoNormal} style={{ overflow: 'hidden' }}>
                  <div className={e.formulario}>
                    {a.formulario}
                    <Button appearance={a.tom === 'destrutivo' ? 'secondary' : 'primary'} className={a.tom === 'destrutivo' ? e.botaoDestrutivo : undefined} disabled={processando}
                      onClick={async () => { await a.aoExecutar(); setAberta(null); }}>
                      {a.rotuloExecutar ?? a.rotulo}
                    </Button>
                  </div>
                </motion.div>
              )}
            </AnimatePresence>
          </div>
        );
      })}
    </div>
  );
}
```

- [ ] **Step 3: Exportar** em `index.ts`:

```ts
export { DetailPageLayout, type DetailPageLayoutProps } from './layout/DetailPageLayout/DetailPageLayout';
export { WorkflowActions, type WorkflowActionsProps, type AcaoWorkflow } from './layout/WorkflowActions/WorkflowActions';
```

- [ ] **Step 4: Galeria** — adicionar:

```tsx
      <Secao titulo="DetailPageLayout + WorkflowActions">
        <div className={larguraTotal}>
          <DetailPageLayout
            cabecalho={{ titulo: 'NC-2026-0042', subtitulo: 'Andaime sem guarda-corpo no pavimento 3.', status: <StatusChip tom="info">Em tratamento</StatusChip>, voltarPara: '/ui-galeria', rotuloVoltar: 'Não conformidades', acoes: <Button appearance="primary">Salvar</Button> }}
            lateral={<>
              <Card densidade="compacta" titulo="Resumo"><span className={tipo.corpo}>Ponte Rio Cuiá, pavimento 3</span></Card>
              <Card densidade="compacta" titulo="Ações disponíveis">
                <WorkflowActions acoes={[
                  { chave: 'concluir', rotulo: 'Concluir tratamento', descricao: 'Registra a evidência e envia para validação', tom: 'primario', formulario: <Field label="O que foi feito"><Textarea /></Field>, aoExecutar: () => setUltimaAcao('concluiu'), rotuloExecutar: 'Concluir e enviar' },
                  { chave: 'prazo', rotulo: 'Pedir mais prazo', descricao: 'Justifique e proponha nova data', formulario: <Field label="Nova data"><CampoData value="" onChange={() => {}} /></Field>, aoExecutar: () => setUltimaAcao('pediu prazo') },
                  { chave: 'devolver', rotulo: 'Devolver ao emitente', tom: 'destrutivo', formulario: <Field label="Motivo"><Textarea /></Field>, aoExecutar: () => setUltimaAcao('devolveu') },
                  { chave: 'encerrar', rotulo: 'Encerrar', descricao: 'Só após validação', habilitada: false, aoExecutar: () => {} },
                ]} />
                <span className={tipo.legenda}>Última ação: {ultimaAcao ?? '—'}</span>
              </Card>
            </>}
          >
            <Card><FormSection titulo="Registro" numero={1} primeira><FormGrid><Campo span={6}><Field label="Origem"><Input value="Inspeção de rotina" readOnly /></Field></Campo><Campo span={6}><Field label="Prioridade"><Input value="Alta" readOnly /></Field></Campo></FormGrid></FormSection></Card>
          </DetailPageLayout>
        </div>
      </Secao>
```
No topo: `const [ultimaAcao, setUltimaAcao] = useState<string | null>(null);`.

- [ ] **Step 5: Verificar** — lateral à direita em tela larga, acima em tela < 1100px; clicar numa ação abre o formulário crescendo do botão e fecha as outras; "Encerrar" desabilitado; "Devolver" em vermelho.

- [ ] **Step 6: Commit**

```bash
cd C:/Projetos/SST-APP && git add src/AAHBRANT.SST.TeamsApp/src/ui && git commit -m "feat(ui): DetailPageLayout e WorkflowActions com formulário inline

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 13: `graficos/paleta.ts` + re-export dos charts

**Files:**
- Create: `src/ui/graficos/paleta.ts`
- Create: `src/ui/graficos/index.ts`
- Modify: `src/ui/index.ts`, `src/ui/galeria/GaleriaPage.tsx`

**Interfaces:**
- Produces: `usePaletaGraficos(): { ok: string; atencao: string; alerta: string; info: string; neutro: string; chrome: string; marca: string; serie: string[] }` — valores hex **resolvidos** em tempo de execução a partir das CSS vars (Recharts não aceita `var()` em `fill`/`stroke`), reagindo à troca de tema.

- [ ] **Step 1: `paleta.ts`**

```ts
import { useEffect, useState } from 'react';

// Paleta para Recharts (spec §1.6): lê as CSS custom properties em tempo de execução, porque SVG
// de gráfico não aceita var() em fill/stroke. Observa data-theme na raiz para acompanhar a troca de tema.
function ler(nome: string): string {
  return getComputedStyle(document.documentElement).getPropertyValue(nome).trim();
}

function resolver() {
  const ok = ler('--sst-status-ok-tinta'), atencao = ler('--sst-status-atencao-tinta'), alerta = ler('--sst-status-alerta-tinta');
  const info = ler('--sst-status-info-tinta'), neutro = ler('--sst-status-neutro-tinta');
  const chrome = ler('--sst-chrome-ativo-fundo'), marca = ler('--sst-color-primary');
  return { ok, atencao, alerta, info, neutro, chrome, marca, serie: [chrome, info, atencao, marca, ok, neutro] };
}

export type PaletaGraficos = ReturnType<typeof resolver>;

export function usePaletaGraficos(): PaletaGraficos {
  const [paleta, setPaleta] = useState<PaletaGraficos>(resolver);
  useEffect(() => {
    const obs = new MutationObserver(() => setPaleta(resolver()));
    obs.observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] });
    return () => obs.disconnect();
  }, []);
  return paleta;
}
```

- [ ] **Step 2: `graficos/index.ts`** (os quatro charts ficam onde estão até a Onda 3; aqui só re-export)

```ts
export { usePaletaGraficos, type PaletaGraficos } from './paleta';
export { RankingBarChart } from '../../components/dashboard/charts/RankingBarChart';
export { StatusDonutChart, type FatiaDonut } from '../../components/dashboard/charts/StatusDonutChart';
export { TrendBarChart, type PontoTendencia } from '../../components/dashboard/charts/TrendBarChart';
export { TrendLineChart } from '../../components/dashboard/charts/TrendLineChart';
```
(Se algum desses módulos não exportar o tipo com esse nome, ajuste o re-export para o nome real que o arquivo exporta — verifique com `grep -n "^export" src/components/dashboard/charts/*.tsx`.)

- [ ] **Step 3: Exportar** em `index.ts`: `export * from './graficos';`

- [ ] **Step 4: Galeria** — adicionar:

```tsx
      <Secao titulo="Paleta de gráficos (segue o tema)">
        <div style={{ display: 'flex', gap: 8 }}>
          {Object.entries(paleta).filter(([k]) => k !== 'serie').map(([k, v]) => <div key={k} style={{ textAlign: 'center' }}><div style={{ width: 56, height: 36, borderRadius: 6, background: v as string }} /><span className={tipo.micro}>{k}</span></div>)}
        </div>
        <Card className={largura360} titulo="Aptidão ocupacional"><StatusDonutChart dados={[{ rotulo: 'Aptos', valor: 256, cor: paleta.ok }, { rotulo: 'Restrição', valor: 24, cor: paleta.atencao }, { rotulo: 'Inaptos', valor: 7, cor: paleta.alerta }]} legendaCentral="funcionários" /></Card>
      </Secao>
```
No topo: `const paleta = usePaletaGraficos();`.

- [ ] **Step 5: Verificar** — as amostras trocam de valor ao alternar o tema; o donut usa as cores da paleta.

- [ ] **Step 6: Commit**

```bash
cd C:/Projetos/SST-APP && git add src/AAHBRANT.SST.TeamsApp/src/ui && git commit -m "feat(ui): paleta de gráficos resolvida das CSS vars e re-export dos charts

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 14: Snapshots visuais com Playwright

**Files:**
- Modify: `package.json` (devDependency + script)
- Create: `playwright.config.ts`
- Create: `tests-ui/galeria.spec.ts`
- Modify: `.gitignore` (raiz do repo) — adicionar `test-results/` e `playwright-report/`

**Interfaces:**
- Produces: `npm run ui:snapshots` (compara) e `npm run ui:snapshots -- --update-snapshots` (regrava). Referências em `tests-ui/galeria.spec.ts-snapshots/`.

- [ ] **Step 1: Instalar**

```bash
npm i -D @playwright/test && npx playwright install chromium
```

- [ ] **Step 2: Scripts em `package.json`** — dentro de `"scripts"`:

```json
    "ui:snapshots": "playwright test -c playwright.config.ts"
```

- [ ] **Step 3: `playwright.config.ts`**

```ts
import { defineConfig } from '@playwright/test';

// Snapshots visuais da galeria ui/ (spec §6). Sobe o Vite, abre /#/ui-galeria nos dois temas e compara
// cada <section> com a referência commitada. Falhou = mudança visual: intencional (--update-snapshots)
// ou regressão.
export default defineConfig({
  testDir: './tests-ui',
  timeout: 60_000,
  expect: { toHaveScreenshot: { maxDiffPixelRatio: 0.002, animations: 'disabled' } },
  use: { viewport: { width: 1280, height: 900 }, baseURL: 'http://localhost:5173', reducedMotion: 'reduce' },
  webServer: { command: 'npm run dev -- --port 5173', url: 'http://localhost:5173', reuseExistingServer: true, timeout: 60_000 },
  projects: [{ name: 'chromium', use: { browserName: 'chromium' } }],
});
```

- [ ] **Step 4: `tests-ui/galeria.spec.ts`**

```ts
import { test, expect } from '@playwright/test';

for (const tema of ['light', 'dark'] as const) {
  test(`galeria em tema ${tema}`, async ({ page }) => {
    await page.addInitScript((t) => localStorage.setItem('sst.modoTema', t), tema);
    await page.goto('/#/ui-galeria');
    await page.waitForSelector('section');
    // Fecha painéis/diálogos que porventura estejam abertos e espera a fonte.
    await page.evaluate(() => document.fonts.ready);
    const secoes = page.locator('main section');
    const total = await secoes.count();
    expect(total).toBeGreaterThan(10);
    for (let i = 0; i < total; i++) {
      const s = secoes.nth(i);
      const titulo = (await s.locator('h2').innerText()).replace(/[^a-z0-9]+/gi, '-').toLowerCase();
      await s.scrollIntoViewIfNeeded();
      await expect(s).toHaveScreenshot(`${tema}-${String(i).padStart(2, '0')}-${titulo}.png`);
    }
  });
}
```

- [ ] **Step 5: Gerar referências e rodar**

```bash
npm run ui:snapshots -- --update-snapshots && npm run ui:snapshots
```
Esperado: primeira execução grava ~36 PNGs; segunda passa com 0 diferenças.

- [ ] **Step 6: `.gitignore` da raiz** — acrescentar:

```
src/AAHBRANT.SST.TeamsApp/test-results/
src/AAHBRANT.SST.TeamsApp/playwright-report/
```

- [ ] **Step 7: Commit**

```bash
cd C:/Projetos/SST-APP && git add .gitignore src/AAHBRANT.SST.TeamsApp/package.json src/AAHBRANT.SST.TeamsApp/package-lock.json src/AAHBRANT.SST.TeamsApp/playwright.config.ts src/AAHBRANT.SST.TeamsApp/tests-ui && git commit -m "chore(ui): snapshots visuais da galeria com Playwright

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 15: Fechar a Onda 0 — PR e merge

- [ ] **Step 1: Verificação final da onda**

```bash
npx tsc -b && npx oxlint && npm run ui:snapshots && npm run build
```
Esperado: tudo verde. `git diff master --stat` mostra **apenas** `src/ui/**`, `tests-ui/**`, `App.tsx`, `index.css`, `theme.ts`, `tsconfig.app.json`, `vite.config.ts`, `.oxlintrc.json`, `package*.json`, `playwright.config.ts`, `.gitignore`.

- [ ] **Step 2: Abrir PR**

```bash
cd C:/Projetos/SST-APP && git push -u origin feature/ui-onda-0 && gh pr create --title "feat(ui): Onda 0 — camada src/ui/ completa, galeria e snapshots" --body "$(cat <<'EOF'
Implementa a Onda 0 da spec docs/superpowers/specs/2026-09-07-sistema-de-design-design.md.

- Aditiva: nenhuma página muda. Tokens novos convivem com os antigos.
- 18 componentes em src/ui/, todos visíveis em /#/ui-galeria (dev).
- Lint no-restricted-imports em warn para pages/.
- Snapshots visuais claro/escuro com Playwright.

Worktrees ativas podem fazer rebase sem conflito.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 3:** Após aprovação e merge em `master`, cada worktree ativa roda `git rebase master` — esperado sem conflito.

---

## Onda 1 — Pilotos

Regra da onda (spec §5.1): se uma peça não servir a um caso real do piloto, **ajusta-se a peça em `src/ui/`** (com galeria e snapshot atualizados no mesmo PR), não se contorna no piloto. Cada piloto é uma branch a partir de `master` pós-Onda 0, PR próprio, verificação no navegador com a API rodando (`dotnet run` no projeto Api) e em homologação antes do merge.

Tabela de substituição comum aos três pilotos:

| Antes | Depois |
|---|---|
| `import { … } from '@fluentui/react-components'` | `import { … } from '@ui'` (só os re-exportados) |
| `usePageStyles()` + `estilos.card` | `<Card>` |
| `estilos.toolbar` + `<Text weight="semibold">` | `<PageHeader>` (ou prop `titulo` de `Card`) |
| `<Text className={estilos.erro}>{erro}</Text>` | `<FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>` |
| `estilos.sectionTitle` (+`First`) | `<FormSection titulo numero primeira>` |
| `estilos.formGrid` + `estilos.colN` | `<FormGrid>` + `<Campo span={N}>` |
| `estilos.formActions` | `<FormRodape>` |
| `<Table>…</Table>` do Fluent | `<DataTable colunas linhas chaveLinha …>` |
| `<Badge color="danger" appearance="tint">` | `<StatusChip tom="alerta">` |
| `<Select>` com > 15 opções | `<SeletorPesquisavel>` |
| `useConfirmarExclusao()` | `useConfirmar()` |
| `Checkbox className={estilosChip.chip}` em lista | `<ChipCheckboxGroup>` |

---

### Task 16: Piloto 1 — `EntregasTab` (lista + painel lateral)

**Files:**
- Modify: `src/pages/epi/EntregasTab.tsx` (519 linhas → reescrita da parte de render; toda a lógica de estado/API permanece)
- Modify: `src/pages/epi/EpiPage.tsx` (abas via `Abas` + `useAbaNaUrl`)

**Interfaces:**
- Consumes: `Card`, `PageHeader`, `DataTable`, `StatusChip`, `nivelVencimento/tomDeVencimento/rotuloDeVencimento`, `PainelLateral`, `FormSection`, `FormGrid`, `Campo`, `FormRodape`, `SeletorPesquisavel`, `FeedbackInline`, `Abas`, `useAbaNaUrl`, `Field`, `Input`, `Select`, `Button`, `CampoData`.
- Mantém a prop `aoNavegarParaMatriz: () => void` e os dois diálogos de assinatura existentes.

- [ ] **Step 1: Branch**

```bash
cd C:/Projetos/SST-APP && git checkout master && git pull --ff-only && git checkout -b feature/ui-piloto-entregas
```

- [ ] **Step 2: `EpiPage.tsx` — abas sincronizadas com `?aba=`**

Substituir o corpo por:

```tsx
import { Abas, useAbaNaUrl, PageHeader } from '@ui';
import { CatalogoTab } from './CatalogoTab';
import { EntregasTab } from './EntregasTab';
import { EstoqueTab } from './EstoqueTab';
import { MatrizEpiTab } from './MatrizEpiTab';

const ABAS = ['entregas', 'catalogo', 'estoque', 'matriz'] as const;
type AbaEpi = (typeof ABAS)[number];

// Módulo EPI (ver comentário anterior sobre por que fica fora do perfil da pessoa). Piloto 1 da camada
// ui/ (spec §5.1): abas passam a viver na URL (?aba=), então voltar e F5 preservam a aba.
export function EpiPage({ mostrarTitulo = true }: { mostrarTitulo?: boolean } = {}) {
  const [aba, setAba] = useAbaNaUrl<AbaEpi>('aba', ABAS, 'entregas');
  return (
    <div>
      {mostrarTitulo && <PageHeader titulo="EPI — Equipamentos de Proteção Individual" />}
      <Abas nivel={mostrarTitulo ? 'pilar' : 'modulo'} valor={aba} aoMudar={setAba} aria-label="Seções de EPI"
        abas={[{ valor: 'entregas', rotulo: 'Entregas' }, { valor: 'catalogo', rotulo: 'Catálogo' }, { valor: 'estoque', rotulo: 'Estoque' }, { valor: 'matriz', rotulo: 'Matriz de EPI por Função' }]} />
      {aba === 'entregas' && <EntregasTab aoNavegarParaMatriz={() => setAba('matriz')} />}
      {aba === 'catalogo' && <CatalogoTab />}
      {aba === 'estoque' && <EstoqueTab />}
      {aba === 'matriz' && <MatrizEpiTab />}
    </div>
  );
}
```

- [ ] **Step 3: `EntregasTab.tsx` — imports e estado novo**

Trocar o bloco de imports do Fluent e de `pageStyles` por:

```tsx
import {
  Button, Field, Input, Select, CampoData,
  Card, PageHeader, DataTable, StatusChip, nivelVencimento, tomDeVencimento, rotuloDeVencimento,
  PainelLateral, FormSection, FormGrid, Campo, FormRodape, SeletorPesquisavel, FeedbackInline, EstadoVazio,
  type Coluna,
} from '@ui';
```
Remover `const estilos = usePageStyles();`. Adicionar estado `const [painelAberto, setPainelAberto] = useState(false);`. Manter todos os outros estados e funções (`carregar`, `criar`, `iniciarDevolucao`, `confirmarDevolucao`, `baixarFicha`, efeitos de NR-6 e EPIs permitidos) **inalterados**. Em `criar()`, após `await carregar();`, acrescentar `setPainelAberto(false);`.

- [ ] **Step 4: `EntregasTab.tsx` — colunas da tabela** (antes do `return`)

```tsx
  const colunas: Coluna<EntregaEpi>[] = [
    { chave: 'trabalhador', rotulo: 'Funcionário', render: (e) => nomeTrabalhador(e.trabalhadorId) },
    { chave: 'epi', rotulo: 'EPI', render: (e) => (
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <FotoCatalogoEpi catalogoEpiId={e.catalogoEpiId} temFoto={epiTemFoto(e.catalogoEpiId)} tamanho={28} />
          {nomeEpi(e.catalogoEpiId)}
        </div>
      ) },
    { chave: 'quantidade', rotulo: 'Qtd.', alinhar: 'direita', largura: '64px' },
    { chave: 'dataEntrega', rotulo: 'Entrega', render: (e) => e.dataEntrega?.slice(0, 10) },
    { chave: 'validade', rotulo: 'Validade', render: (e) => {
        if (e.dataDevolucao) return <StatusChip tom="neutro">Devolvido {e.dataDevolucao.slice(0, 10)}</StatusChip>;
        const n = nivelVencimento(e.dataValidade);
        return n ? <StatusChip tom={tomDeVencimento(n)}>{rotuloDeVencimento(n)}</StatusChip> : '—';
      } },
    { chave: 'devolucao', rotulo: 'Devolução', render: (e) =>
        devolucaoId === e.id ? (
          <div style={{ display: 'flex', gap: 4, alignItems: 'center' }}>
            <CampoData value={devolucaoData} onChange={(_, d) => setDevolucaoData(d.value)} style={{ width: 130 }} />
            <Input type="number" value={devolucaoQtd} onChange={(_, d) => setDevolucaoQtd(d.value)} style={{ width: 60 }} />
            <Button size="small" appearance="primary" onClick={() => confirmarDevolucao(e)} disabled={carregando}>Confirmar</Button>
          </div>
        ) : e.dataDevolucao ? null : (
          <Button size="small" appearance="subtle" onClick={() => iniciarDevolucao(e)}>Registrar devolução</Button>
        ) },
  ];
```

- [ ] **Step 5: `EntregasTab.tsx` — novo `return`** (substitui as linhas 249–517)

```tsx
  return (
    <div>
      <PageHeader
        titulo="Entregas de EPI"
        subtitulo={`${entregas.filter((e) => !e.dataDevolucao).length} entregas ativas`}
        acoes={<Button appearance="primary" icon={<Add24Regular />} onClick={() => setPainelAberto(true)}>Nova entrega</Button>}
      />
      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

      <Card densidade="compacta">
        <DataTable
          aria-label="Entregas de EPI"
          colunas={colunas}
          linhas={entregas}
          chaveLinha={(e) => e.id}
          carregando={carregandoLista}
          vazio={{ titulo: 'Nenhuma entrega registrada', descricao: 'Registre a primeira entrega para começar o controle de EPI.', acao: { rotulo: 'Nova entrega', aoClicar: () => setPainelAberto(true) } }}
          acoesLinha={(e) => (
            <>
              <Button appearance="subtle" size="small" icon={<Signature24Regular />} onClick={() => navigate(`/epi/${e.id}/assinar`)} aria-label="Assinar ficha" title="Assinar ficha" />
              <Button appearance="subtle" size="small" icon={<ArrowDownload24Regular />} onClick={() => baixarFicha(e.trabalhadorId)} disabled={baixandoId === e.trabalhadorId} aria-label="Baixar ficha do funcionário" title="Baixar ficha de EPI em PDF" />
            </>
          )}
        />
      </Card>

      <PainelLateral aberto={painelAberto} aoFechar={() => setPainelAberto(false)} titulo="Nova entrega de EPI" subtitulo="Nada é salvo até você registrar." largura="lg"
        rodape={<><Button onClick={() => setPainelAberto(false)}>Cancelar</Button><Button appearance="primary" onClick={criar} disabled={carregando}>Registrar entrega</Button></>}>
        <FormSection titulo="Quem recebe" numero={1} primeira>
          <FormGrid>
            <Campo>
              <Field label="Funcionário" required>
                <SeletorPesquisavel aria-label="Funcionário" placeholder={`Buscar entre ${trabalhadores.length} funcionários`}
                  opcoes={trabalhadores.map((t) => ({ id: t.id, rotulo: t.nome, descricao: t.matricula }))}
                  valor={novaEntrega.trabalhadorId}
                  aoMudar={(id) => setNovaEntrega({ ...novaEntrega, trabalhadorId: id, catalogoEpiId: '', numeroListaPresencaNr6: '', dataTreinamentoNr6: '' })} />
              </Field>
            </Campo>
          </FormGrid>
        </FormSection>
        <FormSection titulo="O que é entregue" numero={2}>
          <FormGrid>
            <Campo>
              <Field label="EPI" required>
                <Select value={novaEntrega.catalogoEpiId} onChange={(_, d) => setNovaEntrega({ ...novaEntrega, catalogoEpiId: d.value })} disabled={!novaEntrega.trabalhadorId || episPermitidos.length === 0}>
                  <option value="">Selecione</option>
                  {episPermitidos.map((e) => <option key={e.id} value={e.id}>{e.nome} (estoque total: {e.saldoTotal})</option>)}
                </Select>
              </Field>
              {novaEntrega.trabalhadorId && episPermitidos.length === 0 && (
                <FeedbackInline tom="aviso" acao={{ rotulo: 'Cadastrar na matriz', aoClicar: () => { setPainelAberto(false); aoNavegarParaMatriz(); } }}>Esta função não tem EPIs cadastrados na matriz.</FeedbackInline>
              )}
            </Campo>
            <Campo span={4}><Field label="Quantidade"><Input type="number" value={String(novaEntrega.quantidade)} onChange={(_, d) => setNovaEntrega({ ...novaEntrega, quantidade: Number(d.value) })} /></Field></Campo>
            <Campo span={4}><Field label="Data de entrega"><CampoData value={novaEntrega.dataEntrega} onChange={(_, d) => setNovaEntrega({ ...novaEntrega, dataEntrega: d.value })} /></Field></Campo>
            <Campo span={4}><Field label="Validade"><CampoData value={novaEntrega.dataValidade ?? ''} onChange={(_, d) => setNovaEntrega({ ...novaEntrega, dataValidade: d.value })} /></Field></Campo>
            <Campo span={6}>
              <Field label="Motivo">
                <Select value={novaEntrega.motivoTipo} onChange={(_, d) => setNovaEntrega({ ...novaEntrega, motivoTipo: Number(d.value) })}>
                  {Object.entries(motivoEntregaEpiLabel).map(([v, r]) => <option key={v} value={v}>{r}</option>)}
                </Select>
              </Field>
            </Campo>
            <Campo span={6}><Field label="Observação do motivo"><Input value={novaEntrega.motivo ?? ''} onChange={(_, d) => setNovaEntrega({ ...novaEntrega, motivo: d.value })} /></Field></Campo>
          </FormGrid>
        </FormSection>
        <FormSection titulo="Documentação NR-6" numero={3}>
          <FormGrid>
            <Campo span={6}><Field label="Nº lista de presença" hint="Preenchido do treinamento de NR-06 cadastrado, se houver."><Input value={novaEntrega.numeroListaPresencaNr6 ?? ''} onChange={(_, d) => setNovaEntrega({ ...novaEntrega, numeroListaPresencaNr6: d.value })} /></Field></Campo>
            <Campo span={6}><Field label="Data do treinamento"><CampoData value={novaEntrega.dataTreinamentoNr6 ?? ''} onChange={(_, d) => setNovaEntrega({ ...novaEntrega, dataTreinamentoNr6: d.value })} /></Field></Campo>
            <Campo span={6}><Field label="Visto do consórcio/responsável"><Input value={novaEntrega.vistoConsorcioResponsavel ?? ''} onChange={(_, d) => setNovaEntrega({ ...novaEntrega, vistoConsorcioResponsavel: d.value })} /></Field></Campo>
            <Campo span={6}><Field label="Observações"><Input value={novaEntrega.observacoes ?? ''} onChange={(_, d) => setNovaEntrega({ ...novaEntrega, observacoes: d.value })} /></Field></Campo>
          </FormGrid>
        </FormSection>
      </PainelLateral>

      {entregaParaAssinar && ( /* inalterado */ <AssinaturaEntregaEpiDialog open={!!entregaParaAssinar} onClose={() => setEntregaParaAssinar(null)} entregaId={entregaParaAssinar.id} trabalhadorNome={nomeTrabalhador(entregaParaAssinar.trabalhadorId)} epiNome={nomeEpi(entregaParaAssinar.catalogoEpiId)} catalogoEpiId={entregaParaAssinar.catalogoEpiId} epiTemFoto={epiTemFoto(entregaParaAssinar.catalogoEpiId)} quantidade={entregaParaAssinar.quantidade} dataEntrega={entregaParaAssinar.dataEntrega} numeroListaPresencaNr6={entregaParaAssinar.numeroListaPresencaNr6} dataTreinamentoNr6={entregaParaAssinar.dataTreinamentoNr6} /> )}
      {devolucaoParaAssinar && ( /* inalterado */ <AssinaturaDevolucaoEpiDialog open={!!devolucaoParaAssinar} onClose={() => setDevolucaoParaAssinar(null)} entregaId={devolucaoParaAssinar.id} trabalhadorNome={nomeTrabalhador(devolucaoParaAssinar.trabalhadorId)} epiNome={nomeEpi(devolucaoParaAssinar.catalogoEpiId)} quantidadeDevolucao={devolucaoParaAssinar.quantidadeDevolucao ?? devolucaoParaAssinar.quantidade} dataDevolucao={devolucaoParaAssinar.dataDevolucao ?? ''} /> )}
    </div>
  );
```
Adicionar estado `const [carregandoLista, setCarregandoLista] = useState(true);` e em `carregar()`: `setCarregandoLista(true)` no início, `setCarregandoLista(false)` no `finally`. Remover a função local `vencido()` (substituída por `nivelVencimento`) e o import de `Badge`, `Table*`, `Text`.

- [ ] **Step 6: Verificar**

```bash
npx tsc -b && npx oxlint src/pages/epi && npm run dev
```
Esperado: zero avisos `no-restricted-imports` em `src/pages/epi/EntregasTab.tsx` e `EpiPage.tsx`. No navegador, com a API rodando: Operação → EPI e EPC → Entregas mostra a lista como cartões; "Nova entrega" abre o painel; escolher funcionário pela busca preenche NR-6; registrar fecha o painel, recarrega a lista e abre o diálogo de assinatura; trocar de aba muda `?aba=`; F5 mantém. Fluxo de devolução inline continua funcionando.

- [ ] **Step 7: Commit e PR**

```bash
cd C:/Projetos/SST-APP && git add src/AAHBRANT.SST.TeamsApp/src/pages/epi/EntregasTab.tsx src/AAHBRANT.SST.TeamsApp/src/pages/epi/EpiPage.tsx && git commit -m "refactor(epi): EntregasTab e EpiPage na camada ui/ (piloto 1)

Lista vira DataTable com chips de vencimento; formulário de criação sai
de cima da tabela para PainelLateral; funcionário via SeletorPesquisavel;
abas sincronizadas com ?aba=.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>" && git push -u origin feature/ui-piloto-entregas && gh pr create --fill
```

---

### Task 17: Piloto 2 — `NaoConformidadeDetalhePage` (detalhe + workflow)

**Files:**
- Modify: `src/pages/naoconformidades/NaoConformidadeDetalhePage.tsx` (render reescrito; lógica inalterada)

**Interfaces:**
- Consumes: `DetailPageLayout`, `WorkflowActions` (`AcaoWorkflow`), `Card`, `StatusChip`, `FormSection`, `FormGrid`, `Campo`, `FormRodape`, `DataTable`, `FeedbackInline`, `SeletorPesquisavel`, `Carregando`, `Field`, `Input`, `Select`, `Textarea`, `Button`, `CampoData`.

- [ ] **Step 1: Branch** — `git checkout master && git pull --ff-only && git checkout -b feature/ui-piloto-nc`

- [ ] **Step 2: Imports** — substituir o bloco Fluent/pageStyles por:

```tsx
import {
  Button, Field, Input, Select, Textarea, CampoData,
  DetailPageLayout, WorkflowActions, Card, StatusChip, FormSection, FormGrid, Campo, FormRodape, DataTable, FeedbackInline, SeletorPesquisavel, Carregando,
  type AcaoWorkflow, type Coluna,
} from '@ui';
```
Remover `usePageStyles`, `Badge`, `Table*`, `Text`, `ArrowLeft24Regular`, `CheckmarkCircle24Regular`, `useNavigate` (o voltar passa pelo `PageHeader`). Manter todos os estados e funções (`carregar`, `enviar`, `responder`, `registrarConclusao`, `devolver`, `encerrar`, `criarAcao`, `validarAcao`).

- [ ] **Step 3: Tom do status e ações do fluxo** (antes do `return`; mapeia estados → `tom` e monta `AcaoWorkflow[]` só com o permitido no estado atual)

```tsx
  const tomStatus: Record<number, 'neutro' | 'info' | 'atencao' | 'ok' | 'alerta'> = {
    [StatusNaoConformidade.Aberta]: 'neutro',
    [StatusNaoConformidade.Enviada]: 'info',
    [StatusNaoConformidade.Devolvida]: 'alerta',
    [StatusNaoConformidade.EmAndamento]: 'atencao',
    [StatusNaoConformidade.AguardandoValidacao]: 'info',
    [StatusNaoConformidade.Encerrada]: 'ok',
  };

  const opcoesUsuarios = usuarios.map((u) => ({ id: u.id, rotulo: u.nome }));

  const acoes: AcaoWorkflow[] = [];
  if (nc?.status === StatusNaoConformidade.Aberta) {
    acoes.push({ chave: 'enviar', rotulo: 'Enviar ao responsável', descricao: 'Inicia a tratativa', tom: 'primario', aoExecutar: enviar });
  }
  if (nc?.status === StatusNaoConformidade.Enviada || nc?.status === StatusNaoConformidade.Devolvida) {
    acoes.push({ chave: 'responder', rotulo: 'Responder ocorrência', descricao: 'Defina a ação, o executor e o prazo', tom: 'primario', rotuloExecutar: 'Responder', aoExecutar: responder, formulario: (
      <>
        <Field label="Ação a ser realizada" required><Input value={resposta.descricaoAcao} onChange={(_, d) => setResposta({ ...resposta, descricaoAcao: d.value })} /></Field>
        <Field label="Executor"><SeletorPesquisavel aria-label="Executor" placeholder="Manter responsável atual" opcoes={opcoesUsuarios} valor={resposta.responsavelExecucaoId} aoMudar={(id) => setResposta({ ...resposta, responsavelExecucaoId: id })} /></Field>
        <Field label="Prioridade"><Select value={String(resposta.prioridade)} onChange={(_, d) => setResposta({ ...resposta, prioridade: Number(d.value) })}>{Object.entries(prioridadeAcaoLabel).map(([v, r]) => <option key={v} value={v}>{r}</option>)}</Select></Field>
        <Field label="Prazo" hint="Sugerido pela prioridade se em branco"><CampoData value={resposta.prazo} onChange={(_, d) => setResposta({ ...resposta, prazo: d.value })} /></Field>
        <Field label="Justificativa do prazo"><Input value={resposta.justificativaPrazo} onChange={(_, d) => setResposta({ ...resposta, justificativaPrazo: d.value })} /></Field>
      </>
    ) });
  }
  if (nc?.status === StatusNaoConformidade.EmAndamento) {
    acoes.push({ chave: 'concluir', rotulo: 'Registrar conclusão', descricao: 'Envia para validação do inspetor', tom: 'primario', aoExecutar: registrarConclusao, formulario: (
      <Field label="Descrição da conclusão"><Textarea value={descricaoConclusao} onChange={(_, d) => setDescricaoConclusao(d.value)} /></Field>
    ) });
  }
  if (nc?.status === StatusNaoConformidade.AguardandoValidacao) {
    acoes.push({ chave: 'encerrar', rotulo: 'Encerrar', descricao: 'Valida e encerra a não conformidade', tom: 'primario', aoExecutar: encerrar, formulario: (
      <>
        <Field label="Validar como" required><SeletorPesquisavel aria-label="Validador" opcoes={opcoesUsuarios} valor={usuarioValidador} aoMudar={setUsuarioValidador} /></Field>
        <Field label="Observações de encerramento"><Input value={observacoesEncerramento} onChange={(_, d) => setObservacoesEncerramento(d.value)} /></Field>
      </>
    ) });
    acoes.push({ chave: 'devolver', rotulo: 'Devolver ao emitente', descricao: 'A ocorrência volta para quem registrou', tom: 'destrutivo', rotuloExecutar: 'Devolver', aoExecutar: devolver, formulario: (
      <Field label="Motivo da devolução" required><Textarea value={motivoDevolucao} onChange={(_, d) => setMotivoDevolucao(d.value)} /></Field>
    ) });
  }

  const colunasAcoes: Coluna<NaoConformidadeDetalhe['acoesPlano'][number]>[] = [
    { chave: 'tipo', rotulo: 'Tipo', render: (a) => tipoAcaoPlanoLabel[a.tipo] },
    { chave: 'descricao', rotulo: 'Descrição' },
    { chave: 'responsavel', rotulo: 'Responsável', render: (a) => a.responsavelUsuarioNome ?? '—' },
    { chave: 'prioridade', rotulo: 'Prioridade', render: (a) => prioridadeAcaoLabel[a.prioridade] },
    { chave: 'prazo', rotulo: 'Prazo', render: (a) => a.prazo?.slice(0, 10) ?? '—' },
    { chave: 'status', rotulo: 'Situação', render: (a) => <StatusChip tom={a.status === StatusAcaoPlano.Concluido ? 'ok' : 'atencao'}>{statusAcaoPlanoLabel[a.status]}</StatusChip> },
  ];
```
(`nc` deve ser calculado **antes** deste bloco: mover `const nc = detalhe?.naoConformidade;` para cima do `if (!id)`.)

- [ ] **Step 4: Novo `return`** (substitui as linhas 224–522)

```tsx
  if (!id) return <FeedbackInline tom="erro">Não conformidade não encontrada.</FeedbackInline>;
  if (!nc) return <Carregando variante="detalhe" linhas={8} />;

  return (
    <DetailPageLayout
      cabecalho={{
        titulo: nc.descricao,
        subtitulo: [origemNaoConformidadeLabel[nc.origemDeteccao], nc.local, nc.atividadeNome].filter(Boolean).join(', '),
        status: <StatusChip tom={tomStatus[nc.status] ?? 'neutro'}>{statusNaoConformidadeLabel[nc.status]}</StatusChip>,
        voltarPara: '/ocorrencias?secao=nao-conformidades',
        rotuloVoltar: 'Não conformidades',
      }}
      lateral={
        <>
          <Card densidade="compacta" titulo="Resumo">
            <FormGrid>
              {nc.requisitoRelacionado && <Campo span={12}><Field label="Requisito"><Input value={nc.requisitoRelacionado} readOnly /></Field></Campo>}
              <Campo span={12}><Field label="Responsável"><Input value={nc.responsavelUsuarioNome ?? '—'} readOnly /></Field></Campo>
              <Campo span={12}><Field label="Prazo"><Input value={nc.prazo?.slice(0, 10) ?? '—'} readOnly /></Field></Campo>
            </FormGrid>
          </Card>
          {nc.status === StatusNaoConformidade.Devolvida && nc.motivoDevolucao && (
            <FeedbackInline tom="aviso">Motivo da devolução: {nc.motivoDevolucao}</FeedbackInline>
          )}
          {acoes.length > 0 && (
            <Card densidade="compacta" titulo="Ações disponíveis"><WorkflowActions acoes={acoes} processando={processando} /></Card>
          )}
        </>
      }
    >
      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

      <Card titulo="Plano de ação" subtitulo={`${detalhe.acoesPlano.length} ações`}
        acoes={<Field label="Validar como"><SeletorPesquisavel aria-label="Validador" opcoes={opcoesUsuarios} valor={usuarioValidador} aoMudar={setUsuarioValidador} /></Field>}>
        <DataTable aria-label="Ações do plano" densidade="compacta" colunas={colunasAcoes} linhas={detalhe.acoesPlano} chaveLinha={(a) => a.id}
          vazio={{ titulo: 'Nenhuma ação no plano', descricao: 'Adicione a primeira ação abaixo.' }}
          acoesLinha={(a) => a.status !== StatusAcaoPlano.Concluido && !a.dataValidacao ? <Button size="small" appearance="subtle" onClick={() => validarAcao(a.id)} disabled={processando}>Validar</Button> : null} />
        <FormSection titulo="Nova ação" numero={1}>
          <FormGrid>
            <Campo span={2}><Field label="Tipo"><Select value={String(novaAcao.tipo)} onChange={(_, d) => setNovaAcao({ ...novaAcao, tipo: Number(d.value) })}>{Object.entries(tipoAcaoPlanoLabel).map(([v, r]) => <option key={v} value={v}>{r}</option>)}</Select></Field></Campo>
            <Campo span={4}><Field label="Descrição" required><Input value={novaAcao.descricao} onChange={(_, d) => setNovaAcao({ ...novaAcao, descricao: d.value })} /></Field></Campo>
            <Campo span={3}><Field label="Responsável"><SeletorPesquisavel aria-label="Responsável" placeholder="Nenhum" opcoes={opcoesUsuarios} valor={novaAcao.responsavelUsuarioId ?? ''} aoMudar={(id) => setNovaAcao({ ...novaAcao, responsavelUsuarioId: id })} /></Field></Campo>
            <Campo span={2}><Field label="Prioridade"><Select value={String(novaAcao.prioridade)} onChange={(_, d) => setNovaAcao({ ...novaAcao, prioridade: Number(d.value) })}>{Object.entries(prioridadeAcaoLabel).map(([v, r]) => <option key={v} value={v}>{r}</option>)}</Select></Field></Campo>
            <Campo span={1 as never}><Field label="Prazo"><CampoData value={novaAcao.prazo ?? ''} onChange={(_, d) => setNovaAcao({ ...novaAcao, prazo: d.value })} /></Field></Campo>
          </FormGrid>
          <FormRodape info="Ações concluídas exigem validação antes do encerramento da não conformidade."><Button appearance="primary" onClick={criarAcao} disabled={processando}>Adicionar ação</Button></FormRodape>
        </FormSection>
      </Card>
    </DetailPageLayout>
  );
```
**Ajuste de peça obrigatório encontrado neste piloto:** `Campo` só aceita `span` 2/3/4/5/6/12; a linha acima precisa de 1. Em vez de `1 as never`, **ajustar `Campo`** em `src/ui/compostos/Formulario/FormGrid.tsx` para aceitar `1 | 2 | 3 | 4 | 5 | 6 | 8 | 12` (acrescentar `s1: { gridColumn: 'span 1' }` e `s8: { gridColumn: 'span 8' }`), atualizar a galeria e regravar snapshots no mesmo PR. Então usar `<Campo span={1}>`.

- [ ] **Step 5: Verificar**

```bash
npx tsc -b && npx oxlint src/pages/naoconformidades src/ui && npm run ui:snapshots -- --update-snapshots && npm run dev
```
No navegador, com a API: abrir uma NC em cada estado (Aberta, Enviada, Em andamento, Aguardando validação, Encerrada); a lateral mostra só as ações permitidas; cada formulário abre inline; executar recarrega e muda o chip de status; "Devolver" em vermelho; tela < 1100px coloca a lateral acima.

- [ ] **Step 6: Commit e PR**

```bash
cd C:/Projetos/SST-APP && git add src/AAHBRANT.SST.TeamsApp/src && git commit -m "refactor(nc): NaoConformidadeDetalhePage na camada ui/ (piloto 2)

DetailPageLayout com lateral fixa; ações do fluxo em WorkflowActions com
formulário inline; plano de ação em DataTable; Campo aceita span 1 e 8.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>" && git push -u origin feature/ui-piloto-nc && gh pr create --fill
```

---

### Task 18: Piloto 3 — `MatrizEpiTab` (denso + expansível)

**Files:**
- Modify: `src/pages/epi/MatrizEpiTab.tsx`

**Interfaces:**
- Consumes: `Card`, `PageHeader`, `DataTable` (com `expansivel`), `ChipCheckboxGroup`, `FeedbackInline`, `EstadoVazio`, `Button`, `type Coluna`.

- [ ] **Step 1: Branch** — `git checkout master && git pull --ff-only && git checkout -b feature/ui-piloto-matriz-epi`

- [ ] **Step 2: Reescrever o arquivo** (lógica de estado/API preservada; render novo)

```tsx
import { useEffect, useState } from 'react';
import { Button, Card, PageHeader, DataTable, ChipCheckboxGroup, FeedbackInline, type Coluna } from '@ui';
import { api, type CatalogoEpi, type Funcao } from '../../lib/api';

// Matriz de EPI por função: define quais EPIs são obrigatórios para cada função (filtra o seletor de
// EPI em Entregas). Piloto 3 da camada ui/ (spec §4.5): mantém expandir-linha, que o usuário conhece,
// com salvar único no header em vez de um botão por linha.
export function MatrizEpiTab() {
  const [funcoes, setFuncoes] = useState<Funcao[]>([]);
  const [episCatalogo, setEpisCatalogo] = useState<CatalogoEpi[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(true);
  const [expandidoId, setExpandidoId] = useState<string | null>(null);
  const [vinculosSelecionados, setVinculosSelecionados] = useState<string[]>([]);
  const [vinculosOriginais, setVinculosOriginais] = useState<string[]>([]);
  const [salvando, setSalvando] = useState(false);

  async function carregar() {
    try {
      setErro(null); setCarregando(true);
      const [f, e] = await Promise.all([api.funcoes.listar(), api.catalogosEpi.listar()]);
      setFuncoes(f); setEpisCatalogo(e);
    } catch (e) { setErro(e instanceof Error ? e.message : 'Falha ao carregar funções.'); }
    finally { setCarregando(false); }
  }
  useEffect(() => { carregar(); }, []);

  async function alternarExpansao(funcao: Funcao) {
    if (expandidoId === funcao.id) { setExpandidoId(null); return; }
    try {
      setErro(null);
      const vinculados = await api.funcoes.listarEpis(funcao.id);
      const ids = vinculados.map((e) => e.id);
      setVinculosSelecionados(ids); setVinculosOriginais(ids); setExpandidoId(funcao.id);
    } catch (e) { setErro(e instanceof Error ? e.message : 'Falha ao carregar matriz de EPI da função.'); }
  }

  async function salvar() {
    if (!expandidoId) return;
    try {
      setSalvando(true); setErro(null);
      await api.funcoes.definirEpis(expandidoId, vinculosSelecionados);
      setVinculosOriginais(vinculosSelecionados);
      setExpandidoId(null);
    } catch (e) { setErro(e instanceof Error ? e.message : 'Falha ao salvar matriz de EPI.'); }
    finally { setSalvando(false); }
  }

  const alterado = expandidoId !== null && (vinculosSelecionados.length !== vinculosOriginais.length || vinculosSelecionados.some((id) => !vinculosOriginais.includes(id)));

  const colunas: Coluna<Funcao>[] = [
    { chave: 'nome', rotulo: 'Função' },
    { chave: 'cboCodigo', rotulo: 'CBO', largura: '110px' },
    { chave: 'descricao', rotulo: 'Descrição' },
  ];

  return (
    <div>
      <PageHeader titulo="Matriz de EPI por função" subtitulo="Clique numa função para editar os EPIs obrigatórios. Funções são cadastradas em Pessoas."
        acoes={<Button appearance="primary" onClick={salvar} disabled={!alterado || salvando}>{salvando ? 'Salvando…' : 'Salvar alterações'}</Button>} />
      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}
      <Card densidade="compacta">
        <DataTable
          aria-label="Funções e seus EPIs"
          densidade="compacta"
          colunas={colunas}
          linhas={funcoes}
          chaveLinha={(f) => f.id}
          carregando={carregando}
          vazio={{ titulo: 'Nenhuma função cadastrada', descricao: 'Cadastre funções em Pessoas para montar a matriz.' }}
          aoClicarLinha={alternarExpansao}
          expansivel={{
            aberta: (f) => f.id === expandidoId,
            render: () => episCatalogo.length === 0
              ? <span>Nenhum EPI cadastrado no catálogo ainda.</span>
              : <ChipCheckboxGroup aria-label="EPIs obrigatórios" opcoes={episCatalogo.map((e) => ({ id: e.id, rotulo: e.fabricante ? `${e.nome} (${e.fabricante})` : e.nome }))} selecionados={vinculosSelecionados} aoMudar={setVinculosSelecionados} />,
          }}
        />
      </Card>
    </div>
  );
}
```

- [ ] **Step 3: Verificar**

```bash
npx tsc -b && npx oxlint src/pages/epi/MatrizEpiTab.tsx && npm run dev
```
No navegador, com a API: clicar numa função expande com os chips; marcar/desmarcar habilita "Salvar alterações" no header; salvar fecha a linha; reabrir mostra o que foi salvo; trocar de função sem salvar descarta (comportamento igual ao atual).

- [ ] **Step 4: Commit e PR**

```bash
cd C:/Projetos/SST-APP && git add src/AAHBRANT.SST.TeamsApp/src/pages/epi/MatrizEpiTab.tsx && git commit -m "refactor(epi): MatrizEpiTab na camada ui/ (piloto 3)

DataTable expansível com ChipCheckboxGroup e salvar único no header.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>" && git push -u origin feature/ui-piloto-matriz-epi && gh pr create --fill
```

---

### Task 19: Fechar a Onda 1 e preparar o Plano B

- [ ] **Step 1:** Os três PRs de piloto passam por homologação Azure e são mesclados em `master`.

- [ ] **Step 2: Registrar aprendizados** — em `docs/superpowers/specs/2026-09-07-sistema-de-design-design.md`, acrescentar ao final uma seção `## Aprendizados dos pilotos (Onda 1)` listando cada ajuste de peça feito (ex.: "`Campo` ganhou span 1 e 8 — piloto 2") e qualquer caso em que uma peça não serviu.

- [ ] **Step 3: Verificar critérios de pronto parciais** (spec, "Critérios de pronto"): itens 1, 2 e 5 devem estar atendidos; 3, 4 e 6 ficam para o Plano B.

- [ ] **Step 4: Commit**

```bash
cd C:/Projetos/SST-APP && git checkout master && git pull --ff-only && git checkout -b docs/aprendizados-onda-1 && git add docs/superpowers/specs/2026-09-07-sistema-de-design-design.md && git commit -m "docs: aprendizados dos pilotos da Onda 1 do sistema de design

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>" && git push -u origin docs/aprendizados-onda-1 && gh pr create --fill
```

- [ ] **Step 5:** Escrever o **Plano B** (Ondas 2 e 3) com `superpowers:writing-plans`, a partir da spec atualizada.

---

## Auto-revisão do plano

**Cobertura da spec:** §1 tokens → T1; §1.5 reduced-motion → T1; §2.1 estrutura → T1, T4–T13; §2.2 regra de dependência → T2 (lint); §2.3 alias → T1; §2.4 gate → T2; §2.5 nomenclatura → constraints; §3 todos os 18 componentes → T4–T13 (`CampoData`/`ChipsField` re-exportados em T1; charts em T13); §4 templates → exercitados nos pilotos T16 (lista), T17 (detalhe), T18 (matriz) — dashboard e público ficam para a Onda 2; §5 ondas 0–1 → T1–T19; §6 galeria → T3, snapshots → T14; §7 documentação → **Plano B** (Onda 3), exceto aprendizados → T19.

**Placeholders:** nenhum "TBD/TODO". Os dois diálogos de assinatura em T16 são marcados `/* inalterado */` com o código completo reproduzido.

**Consistência de tipos:** `Tom` definido em T1 e usado em T4, T11; `EstadoVazioProps` de T6 usado em T8; `Coluna<T>` de T8 usado em T16–T18; `AcaoWorkflow` de T12 usado em T17; `useAbaNaUrl` de T7 usado em T16; `Campo span` ampliado em T17 (ajuste de peça declarado).

**Desvio conhecido da spec:** a spec §5.1 dizia que `components/`, `hooks/` e `theme.ts` "migram para dentro de `ui/`" na Onda 0. O levantamento pré-plano mostrou 28/83/40 arquivos importando esses caminhos — mover quebraria a promessa aditiva. Este plano re-exporta em vez de mover; a mudança de lugar vai para a Onda 3 (Plano B). Registrar essa correção na spec junto com os aprendizados (T19).

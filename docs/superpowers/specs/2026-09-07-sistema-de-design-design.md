# Sistema de Design (`src/ui/`) — Design

**Data:** 2026-09-07
**Status:** Aprovado pelo usuário em brainstorming (2026-09-07), seção a seção, pronto para plano de implementação.
**Frente:** 1 de 4 (Sistema de design → Fundação de dados → Campo/mobile → Dashboard/indicadores). As outras três têm spec própria quando chegarem.

## Contexto e objetivo

O pedido do usuário foi "elevar e transcender o projeto, design e usabilidade, outro patamar". O levantamento
do frontend (`src/AAHBRANT.SST.TeamsApp`, React 19 + Fluent UI v9 + Vite 8, 167 arquivos, 128 em `pages/`)
mostrou que o problema não é falta de design system — a arquitetura de tokens existente (CSS custom
properties em `index.css` + ponte `designTokens` em `theme.ts` + Griffel) é boa — e sim que **cada tela
estiliza layout à mão**: só 6 componentes compartilhados genéricos para 128 arquivos de página.

Evidência quantificada no levantamento:

- 148 dos 167 arquivos importam `@fluentui/react-components`; 109 importam `@fluentui/react-icons`.
- Sete primitivos Fluent dominam (Text, Button, Field, Table, Badge, Input, Select). Zero uso de
  `DataGrid`, `Card`, `Combobox`, `Toolbar`, `MessageBar`. `Drawer` só via `OverlayDrawer` em 1 arquivo.
- 63 arquivos usam `<Table>` cru.
- 23 sobrescritas de internals do Fluent (`.fui-*`), concentradas em apenas 2 arquivos
  (`index.css`, `pageStyles.ts`).
- 41 cores hex soltas em 14 arquivos de página (dashboards e gráficos).
- 10 tamanhos de fonte à mão (10.5 → 28px) mais `Text size={200|500}` do Fluent; 14 valores de
  espaçamento; 8 raios; 4 sombras; 2 estilos de card.
- 4 sistemas paralelos de cor de status (`useStatusChipStyles`, `BadgeVencimento`, `Badge color=` do
  Fluent, hex via `KpiCard cor=`).
- 1 `@media` no app inteiro.
- framer-motion em 15 arquivos, todos de dashboard.
- 39 `<Select>` sem busca, inclusive para listas de centenas de trabalhadores.
- 5 reestruturações de navegação em 10 dias (comentários em `App.tsx`).

O objetivo desta frente é criar **uma camada de componentes compostos** (`src/ui/`) que as páginas
passam a compor em vez de estilizar à mão, sobre Fluent UI v9 mantido como motor de comportamento e
acessibilidade. Resultado esperado: consistência visual que o olho lê como qualidade, velocidade para
construir tela nova, e um ponto único onde a frente de dados (cache/consulta) entra depois — dentro de
`DataTable`, não em 74 telas.

## Decisões tomadas com o usuário

| Decisão | Escolha | Alternativas descartadas |
|---|---|---|
| Primeira frente | Sistema de design + identidade | Fundação de dados; Campo/mobile; Dashboard |
| Identidade cromática | **Manter verde SST como chrome** (`#1b9b48`/`#10b981` no rail, abas ativas, Administração). Vinho `#670000` segue como marca em CTA/links/traço de aba de pilar. | Preto+bege com vinho de acento; Vinho+bege como marca |
| Sensação-alvo | **Casca arejada, dados densos** — navegação/cabeçalhos/formulários com respiro; tabelas/matrizes/dashboards compactos | Minimalista em tudo; Denso e operacional; Polido e guiado |
| Abordagem | **Fluent como motor + camada própria** em `src/ui/` | Substituir Fluent por headless (148 arquivos, perda do visual Teams); Só tokens |

**Registro explícito:** a regra da organização define a marca como vinho `#670000`, preto, branco e bege
`#ebe9ad`. O usuário escolheu manter verde como cor de navegação/chrome, com vinho reservado a ações e
marca. É uma decisão consciente, não esquecimento — e o item "discrepância de cor" do `ONBOARDING.md` §6
deve ser reescrito para refletir isso (ver seção 7). Bege não entra no app interno.

## 1. Fundamentos

### 1.1 Cor: três famílias, três papéis

Problema: sub-aba selecionada usa `colorSuccess` sólido; rail ativo usa `#1b9b48` sólido; chip "ok" usa
`colorSuccess` lavado. "Onde estou" e "isso está bem" compartilham a cor. Quatro sistemas de status
coexistem.

Decisão — três famílias de token, três papéis:

- **Verde = lugar (chrome).** Nova família `--sst-chrome-{fundo, borda, tinta, ativo-fundo, ativo-tinta}`
  recebe os verdes hoje em `--sst-color-rail-*` e `--sst-color-admin-button-*`. Navegação ativa,
  sub-aba selecionada e botão Administração usam **só** chrome. Sempre preenchimento sólido.
- **Vinho = ação (marca).** `--sst-color-primary #670000` segue em botão primário (via rampa Fluent),
  links e no traço superior da aba de pilar (aprovado 02/09; único lugar em que a marca sela seleção).
- **Semânticas = estado.** `--sst-status-{ok, atencao, alerta, info, neutro}`, cada uma com `-tinta` e
  `-fundo` (lavado). Status é **sempre** fundo lavado + tinta + ícone, **nunca** sólido. O mesmo verde
  convive nas famílias chrome e status porque peso de preenchimento e contexto os separam.
- Um sistema só de status: `StatusChip` (seção 3). `Badge color=` do Fluent proibido em páginas.
- Modo escuro: mecanismo `[data-theme]` atual em `index.css`/`ThemeModeContext.tsx`, sem mudança. Os
  tokens novos ganham valores nos dois blocos.

### 1.2 Tipografia: uma escala

Montserrat fica (já em produção; o doc antigo dizia Inter/Poppins e estava errado). Seis passos, altura
de linha fixa:

| Passo | px / peso / lh | Uso |
|---|---|---|
| `display` | 28 / 800 / 32, `tabular-nums` | valor de KPI |
| `titulo` | 20 / 700 / 26 | título de página (substitui `Text size={500}`) |
| `subtitulo` | 16 / 600 / 22 | título de card e seção |
| `corpo` | 14 / 500 / 20 | padrão (= `fontSizeBase300` do Fluent) |
| `legenda` | 12 / 600 / 16 | rótulos, meta, cabeçalho de tabela |
| `micro` | 11 / 700 / 14, caixa alta, `letter-spacing .05em` | chips, sobretítulos |

Morrem 10.5, 11.5, 12.5, 13, 15 e 26px. A escala é ligada aos tokens do tema Fluent
(`fontSizeBase200..600`, `fontWeight*`, `lineHeightBase*`) em `theme.ts` para que `Text`, `Button`,
`Input` sigam sem wrapper. `ui/tokens/tipografia.ts` exporta os seis passos como classes Griffel.

### 1.3 Espaçamento: grade de 4

`xs 4 · sm 8 · md 12 · lg 16 · xl 24 · 2xl 32 · 3xl 48`. Morrem 2, 6, 10, 14, 18, 20, 28.
Regras: padding de página `xl`; padding de card `xl` (variante densa `lg`); gap entre cards `lg`; gap
interno de formulário `md`; gaps inline `sm`.

### 1.4 Raio e elevação

`sm 6` (inputs, chips pequenos) · `md 10` (botões, linhas de tabela, itens de nav) · `lg 12` (cards,
diálogos, gavetas) · `full 999`. Morrem 8, 9, 16, 20.

**Um card só**: raio `lg`, padding `xl`, sombra `--sst-card-shadow` (existente, por tema). Substitui
`usePageStyles.card` (16px/24-28), `useDashboardStyles.chartCard` e `motorPainel` (12px/18-20) e o card
próprio de `IdentificacaoPublicaPage`. Linhas-como-cartão da `DataTable`: raio `md`, nova
`--sst-row-shadow` mais leve. Botão Administração perde a sombra fixa `0 4px 6px rgba(0,0,0,.3)`.
`borderRadiusSmall/Medium/Large` ligados no tema Fluent.

### 1.5 Movimento: três durações, uma curva, um princípio

`rapido 120ms` (hover, toggle) · `normal 200ms` (troca de aba, chip, expandir linha) · `entrada 300ms`
(montagem de página/card, gaveta). Uma curva: `cubic-bezier(0.2, 0, 0, 1)`.

Princípio: movimento mostra **origem** (gaveta desliza da borda por onde entrou, linha expandida cresce
do cabeçalho) ou **chegada** (cards sobem com fade na montagem, escalonados 40ms) — nunca decoração.
`prefers-reduced-motion: reduce` zera todas as durações.

`ui/tokens/movimento.ts` exporta as durações/curva e presets framer-motion (`entrada`, `escalonado(i)`,
`deslizarDe('direita' | 'baixo')`). Nenhuma página escreve `transition={{ duration: 0.3 }}` à mão.
framer-motion continua como motor (já é dependência).

### 1.6 Fonte de tokens: quem usa o quê

- `src/ui/` pode usar `designTokens` (semântica do app) **e** `tokens` do Fluent (este só para
  estilizar internals do Fluent).
- Páginas usam **nenhum dos dois** diretamente — compõem componentes de `ui/`. Lint garante (seção 2.4).
- Hex cru: zero fora de `index.css` e `theme.ts`.
- Paleta de gráficos: `ui/graficos/paleta.ts` lê `--sst-status-*`, `--sst-chrome-ativo-fundo` e
  `--sst-color-primary` das CSS vars em tempo de execução (`getComputedStyle`), para Recharts seguir o
  tema claro/escuro sozinho.

## 2. Arquitetura de `src/ui/`

### 2.1 Estrutura

```
src/ui/
  tokens/
    tokens.ts        # designTokens migra de theme.ts; ganha famílias chrome/status
    tipografia.ts    # os 6 passos como classes Griffel
    movimento.ts     # durações, curva, presets framer
  primitivos/        # wrapper fino de 1 componente Fluent (ou sem Fluent)
    StatusChip/  SeletorPesquisavel/  ChipCheckboxGroup/  ConfirmDialog/
    FeedbackInline/  Carregando/  EstadoVazio/  CampoData/  ChipsField/
  compostos/         # combinam vários primitivos
    PageHeader/  Abas/  DataTable/  FormSection/  FormGrid/  KpiCard/  PainelLateral/  Card/
  layout/
    DetailPageLayout/  WorkflowActions/
  graficos/
    paleta.ts  RankingBarChart/  StatusDonutChart/  TrendBarChart/  TrendLineChart/
  index.ts           # única porta de saída; também re-exporta os primitivos Fluent permitidos
```

Um componente por pasta (`Nome/Nome.tsx` + `Nome.styles.ts` quando os estilos passarem de ~40 linhas).
`components/`, `hooks/` e `theme.ts` atuais migram para dentro de `ui/` ao longo da seção 5.
`layout/AppShell.tsx` fica fora de `ui/` — é casca da aplicação, não peça reutilizável — mas consome
`ui/tokens`.

### 2.2 Regra de dependência

- `ui/tokens` → nada do app (só Fluent `tokens` e framer).
- `ui/primitivos`, `compostos`, `layout`, `graficos` → Fluent + `ui/tokens` + outros `ui/*`. **Nunca**
  `lib/api`, `pages/`, `teams/`. Componentes recebem dados prontos por props; quem busca é a página.
- `pages/` → `@ui`, `lib/`, React Router. **Nunca** `@fluentui/react-components` direto.
- Exceção declarada: `Text`, `Button`, `Field`, `Input`, `Select`, `Textarea`, `Checkbox`, `Spinner`,
  `Avatar`, `Tooltip` são primitivos Fluent que já estão certos e **não** ganham wrapper. São
  re-exportados por `ui/index.ts`; a página importa `Button` de `@ui`. Se um dia precisarem de skin,
  troca-se num lugar.

### 2.3 Alias

`tsconfig.app.json` não tem `paths`. Adicionar `"baseUrl": "."` e
`"paths": { "@ui": ["src/ui/index.ts"], "@ui/*": ["src/ui/*"] }`, e o `resolve.alias` equivalente em
`vite.config.ts`.

### 2.4 Gate de lint

`.oxlintrc.json` (hoje 2 regras) ganha `no-restricted-imports` com override por pasta:

- em `src/pages/**`: proíbe `@fluentui/react-components` (mensagem: "importe de @ui"),
  `../theme`, `../../theme`, `../../../theme`.
- em `src/ui/**`: proíbe `**/lib/api`, `**/pages/**`.

Nível `warn` durante as ondas 0–2 da migração; `error` na onda 3.

### 2.5 Nomenclatura

Português nos componentes novos; `StatusChip` e `KpiCard` mantêm o nome por já serem termos correntes
no código. Props em português, como o resto do app (`aoFechar`, `carregando`, `tom`). Um componente por
arquivo, arquivo com o nome do componente.

## 3. Inventário de componentes

Cada peça ancorada no padrão que substitui. Props só o essencial; detalhes finos no plano.

### Primitivos

**`StatusChip`** — substitui os 4 sistemas de status. Props: `tom: 'ok' | 'atencao' | 'alerta' | 'info' |
'neutro'`, `icone?`, `children`. Sempre `--sst-status-*-fundo` + `-tinta`. Helper exportado
`nivelVencimento(data): 'vencido' | 'alerta' | 'valido' | null` preserva a regra dos 30 dias de
`BadgeVencimento` e mapeia para `tom` (`valido→ok`, `alerta→atencao`, `vencido→alerta`). Absorve
`BadgeVencimento` e `useStatusChipStyles`.

**`SeletorPesquisavel`** — wrapper de `Combobox` do Fluent (0 usos hoje). Substitui `<Select>` quando a
lista tem mais de ~15 itens (trabalhadores, EPIs, cursos, obras). Props: `opcoes: {id, rotulo,
descricao?}[]`, `valor`, `aoMudar`, `placeholder?`, `vazio?`. Busca por texto no rótulo e descrição.
`<Select>` continua para enums curtos.

**`ChipCheckboxGroup`** — formaliza `useCheckboxChipStyles` (15 arquivos). Props: `opcoes: {id,
rotulo}[]`, `selecionados: string[]`, `aoMudar`, `colunas?`. O chip inteiro é o alvo de clique.

**`ConfirmDialog`** + hook **`useConfirmar()`** — absorve `useConfirmarExclusao` (39 telas), mantendo a
API `await confirmar(mensagem | opcoes)`. Ganha `tom: 'destrutivo' | 'neutro'`: destrutivo usa botão
`--sst-status-alerta` sólido (única exceção à regra "status nunca sólido", por ser ação e não estado),
não `appearance="primary"` vinho.

**`FeedbackInline`** — wrapper de `MessageBar` (0 usos hoje). Substitui `<Text className={estilos.erro}>`.
Props: `tom: 'erro' | 'aviso' | 'sucesso' | 'info'`, `children`, `acao?: {rotulo, aoClicar}`,
`aoFechar?`.

**`Carregando`** — `ListaCarregando` renomeado. Props: `variante: 'lista' | 'card' | 'kpi' | 'detalhe'`,
`linhas?`. Skeletons no formato do conteúdo que substituem.

**`EstadoVazio`** — upgrade. Props: `icone`, `titulo`, `descricao`, `acao?: {rotulo, aoClicar}`,
`variante?: 'vazio' | 'sem-resultado' | 'em-construcao'`. Absorve `EmConstrucaoPage`. Um vazio sem ação é
porta fechada; com ação é convite.

**`CampoData`**, **`ChipsField`** — mantidos como estão, movidos para `ui/primitivos`.

### Compostos

**`PageHeader`** — formaliza o `toolbar` + `<Text size={500} weight="semibold">` presente em toda página.
Props: `titulo`, `subtitulo?`, `acoes?: ReactNode`, `voltarPara?: string`, `status?: ReactNode`,
`filtros?: ReactNode`. Substitui o boolean `mostrarTitulo` espalhado: a página-pilar simplesmente não
renderiza `PageHeader` no filho.

**`Abas`** — encapsula `TabList`. Props: `nivel: 'pilar' | 'modulo' | 'interno'` (os estilos hoje em
`usePillTabStyles`, `useSubTabStyles` e o terceiro nível sem estilo), `abas: {valor, rotulo,
contador?}[]`, `valor`, `aoMudar`, `param?: string`. Quando `param` é dado (`'secao'`, `'aba'`),
**sincroniza com a URL nos dois sentidos** via `useSearchParams` — hoje só lê na montagem; voltar do
navegador e F5 perdem a aba. Máximo três níveis; acima disso é sinal de dividir o módulo.

**`DataTable`** — o maior ganho: 63 arquivos. Props: `colunas: {chave, rotulo, largura?, alinhar?,
render?}[]`, `linhas: T[]`, `chaveLinha: (l: T) => string`, `carregando?`, `vazio?: EstadoVazioProps`,
`densidade: 'confortavel' | 'compacta'`, `aoClicarLinha?`, `acoesLinha?: (l: T) => ReactNode`,
`expansivel?: {aberta: (l: T) => boolean, render: (l: T) => ReactNode}`, `cabecalhoFixo?`. Absorve o hack
global `.fui-TableRow.fui-TableRow` — a linha-como-cartão vira estilo interno do componente e o hack sai
de `index.css`. Internamente continua `Table`/`TableRow` do Fluent (acessibilidade grátis).

**`FormSection`** + **`FormGrid`** + **`Campo`** — formalizam `sectionTitle`/`sectionTitleFirst` e o grid
de 12 colunas (`formGrid`, `col2..col12`). `FormSection titulo numero?`; `FormGrid` com filhos
`<Campo span={6}>`. O breakpoint de 900px (hoje o único `@media` do app) fica dentro de `FormGrid`.

**`KpiCard`** — promovido de `components/dashboard`. Perde `cor: string`, ganha `tom` (mesma união do
`StatusChip`), `delta?: {texto, tom}[]`, `icone`, `carregando?`. Entrada via preset `escalonado`. Mata
os 41 hex soltos.

**`PainelLateral`** — wrapper de `OverlayDrawer`. Props: `aberto`, `aoFechar`, `titulo`, `largura?: 'md'
(400) | 'lg' (560)`, `rodape?`. Absorve o estilo de `TrabalhadoresGaveta`. Movimento `deslizarDe('direita')`.

**`Card`** — o card único de 1.4. Props: `densidade?: 'confortavel' | 'compacta'`, `titulo?`, `subtitulo?`,
`acoes?`. Substitui `estilos.card`, `chartCard`, `motorPainel` e o card da página pública.

### Layout

**`DetailPageLayout`** — o padrão das 6 páginas de detalhe (~500 linhas cada: NC, PCMSO, DDS, Inspeção,
Reunião CIPA, PT). Props: `cabecalho: PageHeaderProps`, `lateral?: ReactNode`, `children`. Lateral fixa à
direita (320px) em telas ≥ 1100px; acima do conteúdo abaixo disso.

**`WorkflowActions`** — extraído de `NaoConformidadeDetalhePage`. Props: `acoes: {chave, rotulo, tom,
aoExecutar, formulario?: ReactNode, habilitada, descricao?}[]`, `processando?`. Renderiza só as ações
permitidas no estado atual; `formulario` abre inline (expansão, preset `entrada`) ao clicar, não em
`Dialog`. PT, APR, Inspeção e NC compartilham a forma.

### Gráficos

Os 4 charts atuais movem para `ui/graficos`; cores vêm de `paleta.ts`. Ganham `carregando` e `vazio`
embutidos e usam `Card` como moldura.

### Fica fora, de propósito

Editor rico, upload genérico, calendário completo (`CalendarioPage` mantém layout próprio), edição
inline em célula de tabela, vista em grade real de matriz (função × item). Entram só se um piloto pedir.

## 4. Templates de página

Cinco formas cobrem as 46 páginas. Nenhum template é componente novo — é composição.

### 4.1 Pilar (4 itens do rail)

```
Abas nivel="pilar"  param="secao"   [PGR/GRO] [PCMSO] [Treinamentos] …
  └ Abas nivel="modulo"  param="aba"   (PGRs) (Matriz) (Atividades) …
      └ conteúdo: Lista | Dashboard | Matriz
```

Mudanças: as duas abas sincronizam com a URL nos dois sentidos. O terceiro nível que hoje aparece em
alguns dashboards vira `nivel="interno"` ou, com 2 opções, um `Switch`.

### 4.2 Lista (~60 telas)

```
PageHeader  titulo · filtros ······················ [+ Novo]
Card
  DataTable densidade="confortavel"
    linhas-como-cartão · StatusChip · acoesLinha
    vazio → EstadoVazio com ação "Cadastrar primeiro"
    carregando → Carregando variante="lista"
```

Mudanças: o formulário de criação sai da mesma tela (hoje fica acima da tabela, empurrando os dados)
e vai para `PainelLateral` aberto pelo `[+ Novo]` — a lista fica visível enquanto se cadastra. Filtros
no header, não num card separado.

### 4.3 Detalhe / Workflow (6 páginas)

```
DetailPageLayout
  cabecalho: voltar · titulo · StatusChip(estado) · ações primárias
  lateral:  Card resumo (obra, responsável, prazos)
            WorkflowActions (ações do estado atual, formulário inline)
  children: FormSection 1 · FormSection 2 · DataTable(itens filhos) …
```

Mudanças: as ações do fluxo saem do meio do conteúdo e ficam sempre visíveis na lateral.

### 4.4 Dashboard (`DashboardPage` + 9 `*DashboardTab`)

```
KpiCard × 4–6   (grid auto-fit, min 185px)
Card gráfico | Card gráfico | MiniCalendar   (grid auto-fit, min 340px)
Card feed     | Card feed
```

Mudanças só de forma. A lógica de cálculo dos KPIs (agregação no cliente de 11 coleções) **não** muda
nesta frente — é assunto da frente de dados.

### 4.5 Matriz (`MatrizEpiTab`, `MatrizTreinamentoTab`, `MatrizRiscoTab`)

```
PageHeader  titulo · [buscar] ·············· [Salvar alterações]
Card densidade="compacta"
  DataTable densidade="compacta" expansivel
    ▸ Função A          12 EPIs
    ▾ Função B          8 EPIs
        ChipCheckboxGroup (itens do catálogo)
```

Mudanças: mantém expandir-linha (funciona, o usuário conhece), com salvar único no header em vez de um
botão por linha. Vista em grade real fica anotada como feature futura.

### 4.6 Público (`/p/:id`, `/validar/:token`)

Sem rail, tema claro forçado, largura máx. 480px. `Card` centralizado: `Avatar` grande, nome, função,
`StatusChip(aptidão)`, lista de itens cada um com `StatusChip`. Só troca `Badge color=` por `StatusChip`
e o card próprio por `Card`. Já é a única tela mobile-friendly do app; o template preserva isso.

### Fora dos templates

`AppShell` (rail + topbar) não muda de forma — só consome os tokens novos. `CalendarioPage` mantém layout
próprio. `AssinaturaQuiosque` é tela de dispositivo dedicado, fica como está.

## 5. Estratégia de migração

O risco é de coordenação, não técnico: 8 worktrees ativas e um incidente de divergência já documentado
no `ONBOARDING.md`. A estratégia garante que nenhuma feature branch precise parar.

### 5.1 Ondas

**Onda 0 — Fundação (aditiva).** `src/ui/` nasce completo com todas as peças da seção 3, mas nenhuma
página o usa. Tokens novos entram em `index.css` ao lado dos antigos. Alias `@ui`, lint em `warn`.
Galeria `/ui-galeria` (seção 6). Merge em `master`. As worktrees fazem rebase sem conflito.

**Onda 1 — Pilotos (3 telas, uma de cada forma difícil).**
- `EntregasTab` (519 linhas): lista + formulário + 5 fetches + 2 diálogos de assinatura. Valida
  `DataTable` + `PainelLateral` + `SeletorPesquisavel`.
- `NaoConformidadeDetalhePage` (524 linhas): workflow mais completo. Valida `DetailPageLayout` +
  `WorkflowActions`.
- `MatrizEpiTab`: caso denso/expansível. Valida `DataTable expansivel` + `ChipCheckboxGroup`.
Cada piloto é branch curta, PR próprio, verificação no navegador. Se uma peça não serviu,
**ajusta-se a peça**, não o piloto.

**Onda 2 — Varredura mecânica.** Os ~60 arquivos restantes com `<Table>`, em lotes de ~8 por PR
agrupados por módulo. Cada PR: `Table→DataTable`, `estilos.card→Card`, `Text size={500}→PageHeader`,
`Badge color=→StatusChip`, `estilos.erro→FeedbackInline`, `useConfirmarExclusao→useConfirmar`.

**Onda 3 — Remoção.** Quando o último `<Table>` cru sair: apagar `usePageStyles.card`, o hack
`.fui-TableRow.fui-TableRow`, os tokens `--sst-color-rail-*`/`--sst-color-admin-button-*` antigos,
`components/` e `hooks/` legados, `EmConstrucaoPage`, `BadgeVencimento`. Lint vira `error`. É o único
PR que quebra quem não migrou — por isso é o último.

### 5.2 Coexistência

Durante as ondas 1–2, tela antiga e nova lado a lado têm visual ligeiramente diferente (raio 16 vs 12,
KPI 26 vs 28). Aceito e deliberado — big-bang foi o que causou o incidente anterior. Nenhum piloto
entra em `master` sem passar pela homologação Azure existente.

### 5.3 Worktrees ativas

Feature nova iniciada após a Onda 0 nasce em `@ui`. Feature já em andamento termina como começou e é
migrada na Onda 2 junto com o módulo dela. Ninguém migra no meio de uma feature.

### 5.4 Riscos

- Peça que não cabe num caso real → aparece no piloto, antes da varredura.
- Fluent atualiza e quebra `.fui-*` → risco hoje em 2 arquivos; passa a 1 (`DataTable`).
- Alguém importa Fluent direto em página nova → lint avisa (ondas 0–2), bloqueia (onda 3).

## 6. Verificação

O frontend não tem teste nenhum hoje. Esta frente cria o mínimo que impede regressão visual silenciosa.

**Galeria em rota dev — `/#/ui-galeria`.** Página interna, só em `import.meta.env.DEV`, que renderiza
cada peça da seção 3 em todos os estados (vazio, carregando, erro, densidades, tons) nos dois temas.
Zero dependência nova; coerente com "implementação real, nunca mockup".

**Snapshots visuais com Playwright.** Script `npm run ui:snapshots` abre a galeria, captura uma imagem
por peça × tema, compara com a referência commitada em `src/ui/__snapshots__/`. Falhou = mudança visual;
intencional (atualiza referência) ou regressão. Roda localmente antes de cada PR de piloto/varredura;
entra no pipeline Azure após a Onda 1. Adiciona `@playwright/test` como devDependency.

**Verificação manual continua obrigatória** (regra do `ONBOARDING.md`). Snapshots cobrem a peça
isolada; o olho cobre a peça no contexto.

**Fora de escopo:** testes unitários de componente, e2e de fluxo. Entram com a frente de dados.

## 7. Documentação

- `DESING SYSTEM AAHBRANT.md` (raiz, nome com erro) → `docs/design-system.md`, reescrito: verde como
  chrome (decisão consciente contra a regra `#670000/#ebe9ad`, registrada), Montserrat, as famílias de
  token, as 4 escalas, os componentes com uma linha de "quando usar" cada.
- `ONBOARDING.md` §5 ganha a regra de dependência de `ui/` e o gate de lint. §6 corrige "discrepância de
  cor": o código já usa `#670000` para marca; a discrepância real é o verde de chrome, agora decisão.
- Comentário de cabeçalho em cada componente: uma frase de propósito + o padrão que substitui, no
  estilo dos comentários já existentes.
- **Não se cria:** wiki, Storybook, ADRs separados. A galeria é a documentação viva; esta spec é o
  histórico da decisão.

## Critérios de pronto desta frente

1. `src/ui/` existe com todas as peças da seção 3, cada uma visível na galeria nos dois temas.
2. Os 3 pilotos da Onda 1 estão em `master`, verificados no navegador e em homologação.
3. Nenhum arquivo em `src/pages/` importa `@fluentui/react-components` diretamente (lint `error` passa).
4. Zero hex fora de `index.css` e `theme.ts`.
5. `npm run ui:snapshots` passa.
6. `docs/design-system.md` e `ONBOARDING.md` atualizados.

## Correções ao levantamento inicial

Registradas para honestidade do histórico:
- framer-motion **está** em uso (15 arquivos de dashboard); a afirmação inicial "instalado e não usado"
  estava errada.
- `Checkbox` **está** em uso (15 arquivos); o grep ancorado em JSX de linha única subestimou.
- O código já usa `#670000` como `colorPrimary`; o único `#7B1E2B` restante é um hex solto em
  `TrabalhadorDetalhePage.tsx:218`. O item do `ONBOARDING.md` §6 está desatualizado.
- A seção 5.1 previa que `components/`, `hooks/` e `theme.ts` "migram para dentro de `ui/`" já na
  Onda 0. O levantamento pré-plano mostrou 28/83/40 arquivos importando esses caminhos — mover
  quebraria a promessa aditiva da onda. O plano de implementação **re-exportou em vez de mover**; a
  mudança de lugar física fica para a Onda 3, junto com a remoção do legado (§5.1).

## Aprendizados dos pilotos (Onda 1)

Os três pilotos (T16 `EntregasTab`, T17 `NaoConformidadeDetalhePage`, T18 `MatrizEpiTab`) validaram a
regra do §5.1 — "se uma peça não serviu, ajusta-se a peça, não o piloto" — na prática: **todo defeito
de componente encontrado foi corrigido dentro de `src/ui/`, nunca contornado na página**. Nenhum piloto
precisou de um caso de "a peça genuinamente não serve" (workaround permanente na página); os casos
abaixo são todos ajustes reais de peça, com galeria e snapshot atualizados junto.

### Ajustes de peça

**`FormGrid`** (piloto 1, reconfirmado de forma independente pelo piloto 2) — `gridTemplateColumns:
repeat(12, 1fr)` vazava horizontalmente em faixas estreitas: `1fr` é `minmax(auto, 1fr)`, então a
largura intrínseca de um `<Input>`/`<Select>` (ou de um `.fui-Field`) nunca deixava a coluna encolher
abaixo dela. Apareceu primeiro no piloto 1 (`PainelLateral` de 560px) e, de forma independente, no
piloto 2 (linha "Nova ação" de `NaoConformidadeDetalhePage`) — duas branches irmãs corrigiram o mesmo
defeito ao mesmo tempo; a reconciliação adotou a versão mais completa (piloto 1), que cobre a cadeia
inteira: `minmax(0, 1fr)` no grid + `minWidth: 0` em `.fui-Field`, nos wrappers do Fluent
(`.fui-Input/.fui-Select/.fui-Combobox/.fui-Textarea/.fui-Dropdown`) e nos elementos nativos.
**Duas branches corrigindo o mesmo defeito de peça ao mesmo tempo é sinal de que o defeito é da peça,
não do caso de uso** — motivo a mais para a regra do §5.1.

**`DetailPageLayout`** (piloto 2) — a media query de 1100px resetava só `order`/`position`; `maxHeight`
e `overflowY` continuavam valendo, e a lateral virava uma caixa de rolagem de altura cheia acima do
conteúdo em telas estreitas. Corrigido resetando os dois eixos juntos (`maxHeight: none`, `overflowY:
visible`) — **regra geral, não só deste componente: com um eixo em `hidden`, o outro `visible` computa
como `auto`**, então um reset parcial não é reset nenhum. `overflowX: hidden` fixado na classe base
(o eixo X estava computando `auto` e criava uma barra de rolagem parasita sob a sombra dos cards).

**`SeletorPesquisavel`** (piloto 2) — ganhou `opcaoVazia?: string`. O `Combobox` do Fluent não tem
equivalente ao `<option value="">` de um `<select>` nativo: sem essa prop, substituir um select que
tinha uma opção vazia semanticamente relevante ("Manter responsável atual", "Nenhum") tirava do usuário
o caminho de volta ao vazio. **Regra de uso**: todo `<select>` com `<option value="">` que vira
`SeletorPesquisavel` precisa declarar `opcaoVazia`, nunca perder essa opção silenciosamente.

**`WorkflowActions`** (piloto 2) — `aoExecutar` passou a aceitar devolver `false` (além de `void` ou
lançar) para manter o formulário de uma ação aberto. Sem isso, uma validação puramente local (que só
faz `setErro` + `return`, sem chamar a API) fechava o acordeão como se tivesse tido sucesso, e a
mensagem de erro ficava fora da vista, apontando para um campo que já não estava mais na tela.

**`DataTable`** (piloto 3, dois ajustes + um achado pós-merge em revisão independente):
1. A área da linha expansível (`expandida`) não tinha `display: flex`/`gap` — funcionava por acaso
   porque nenhum consumidor anterior (só a galeria, com um exemplo de um filho só) passava mais de um
   nó para `expansivel.render()`. `MatrizEpiTab` foi o primeiro caso real com dois filhos (um título de
   contexto + um `ChipCheckboxGroup`) e eles colavam sem espaço. Corrigido com `flexDirection: column`
   + `gap`; `align-items: stretch` (padrão de uma coluna flex) trocado por `flex-start` no mesmo commit
   — sem isso, um `<Button>` passado direto por um consumidor futuro viraria full-width sem pedir.
2. Uma revisão independente, já com o piloto mesclado, achou que `DataTable expansivel` não tinha
   nenhum indicador visual de disclosure — a spec §4.5 desenha `▸`/`▾`, o componente só tinha
   `cursor: pointer`. Corrigido com `ChevronRight16Regular` na primeira coluna (só quando `expansivel`
   está presente), rotacionando 90° quando a linha abre — mesmo padrão já usado em `WorkflowActions`.
   **Este é justamente o componente que o piloto existe para validar; vale reforçar no checklist de
   revisão de qualquer piloto futuro que exercite disclosure**: conferir a afordância visual, não só a
   funcional (`aria-expanded` já estava correto — o gap era só visual).

### Aprendizados de processo e verificação (não são ajuste de peça, mas mudam como pilotos futuros devem verificar)

- **Erro de formulário dentro de um painel/modal precisa de estado PRÓPRIO**, renderizado como o
  primeiro filho do painel — nunca reaproveitar o `erro` de nível de página, que fica atrás do backdrop
  modal e nunca é visto (piloto 1). Fechar o painel deve limpar esse estado em **todos** os caminhos de
  fechamento (X, Cancelar, atalho de navegação), não só no "concluir com sucesso".
- **`StatusChip` nunca substitui sozinho um valor de auditoria** (data, número, contagem) que já
  existia na UI antes da migração — mostrar o valor cru ao lado do chip, não só o chip (piloto 1).
- **Um "nenhum snapshot mudou" só é evidência de correção se o teste efetivamente alcança o estado que
  a correção afeta.** A galeria tinha `?abrir=painel|dialogo` para fotografar estados que só existem
  abertos, mas faltava o equivalente para uma linha expansível: o `DataTable` de exemplo sempre
  iniciava fechado, então o fix do `gap` (item acima) nunca entrou em nenhum PNG — a "prova" de que o
  fix funcionava era, na origem, só a leitura do CSS. Corrigido com `?abrir=expandida`, mesmo padrão
  dos outros dois (piloto 3, achado em revisão pós-merge).
- **Screenshot de uma seção inteira da galeria, em vez de um elemento isolado, pode produzir um PNG
  corrompido** quando a seção é mais alta que a viewport de teste (900px) e contém algo em
  `position: sticky`: o Playwright faz scroll-e-stitch para cobrir a altura toda, e o elemento sticky
  "vaza" congelado no meio da imagem final. Mitigação usada: isolar com `data-testid` só o card do
  exemplo de interesse, em vez de `section[data-secao=...]` inteira, quando a seção mistura múltiplos
  exemplos e algum deles usa `cabecalhoFixo` (piloto 3).
- **Uma decisão de UX que muda ONDE uma ação vive pode criar um risco de perda de dado que não existia
  antes**, mesmo sem tocar a lógica de salvar em si. Mover "Salvar" de dentro de uma linha expansível
  para o `PageHeader` (spec §4.5) tira o botão de perto do que ele salva — o piloto 3 avaliou esse
  efeito colateral deliberadamente e adicionou confirmação de descarte só quando há edição pendente
  (não intrusiva no caminho de leitura, que é o dominante).

### Itens levantados nos pilotos e deliberadamente deferidos para a Onda 2 ou depois

- Escape hatches sem confirmação de descarte na Matriz de EPI (trocar de aba do Fluent, F5, navegação
  pela sidebar) — nenhum piloto tem `beforeunload`/route-guard; decidir na Onda 2 se isso entra na spec
  como regra de template ou fica como limitação aceita.
- `[buscar]` no `PageHeader` e contagem de itens por linha ("12 EPIs") do template §4.5 — não
  implementados no piloto 3; a contagem exigiria N chamadas extras de API ou um endpoint agregado
  novo. A Onda 2 replica este template em `MatrizTreinamentoTab`/`MatrizRiscoTab`; decidir os dois
  juntos antes de repetir o padrão pela terceira vez.
- Erro de carga e `EstadoVazio` aparecendo juntos quando uma lista fica vazia por causa de uma falha de
  rede (ex.: "Failed to fetch" + "Nenhuma função cadastrada" ao mesmo tempo) — padrão herdado do
  piloto 1 e repetido no piloto 3; candidato a virar regra de template (`vazio` só quando
  `erro === null`) na Onda 2.
- Botões de ação de linha em `size="small"` (~24px, só ícone) no piloto 1 — alvo de toque fraco;
  território da frente de campo/mobile, não desta.
- `FormGrid` acopla ao seletor interno `.fui-Field` do Fluent — revisitar num upgrade de versão.

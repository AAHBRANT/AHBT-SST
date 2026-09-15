# Sistema de Design do SST-APP (`src/ui/`)

**Substitui:** `DESING SYSTEM AAHBRANT.md` (raiz do repo, removido — nome com erro de digitação e
conteúdo obsoleto: descrevia o Hub Administrativo Gênesis, com Inter/Poppins e `#7B1E2B`, não o
app de SST que este repositório efetivamente implementa).

**Fonte de verdade:** `docs/superpowers/specs/2026-09-07-sistema-de-design-design.md` (spec
completa, aprovada pelo usuário em brainstorming seção a seção) e
`docs/superpowers/plans/2026-09-08-sistema-de-design-onda-2-3.md` (plano de execução). Este
documento é um resumo de consulta rápida; em caso de divergência, a spec manda.

**A documentação viva é o código.** Cada peça de `src/ui/` tem uma galeria interna
(`/#/ui-galeria`, só em `import.meta.env.DEV`) que mostra a peça em todos os estados, nos dois
temas. Este arquivo não substitui a galeria nem o comentário de cabeçalho de cada componente — é
o ponto de entrada para quem chega no projeto agora.

---

## 1. Decisão de identidade cromática — verde como chrome, vinho como marca

A regra da organização AAHBRANT define a marca como **vinho `#670000`, preto, branco e bege
`#ebe9ad`**. O app de SST **não segue essa regra à risca na navegação** — e isso é intencional,
não um esquecimento. Da spec (seção "Decisões tomadas com o usuário" e "Registro explícito"):

> **Registro explícito:** a regra da organização define a marca como vinho `#670000`, preto,
> branco e bege `#ebe9ad`. O usuário escolheu manter verde como cor de navegação/chrome, com
> vinho reservado a ações e marca. É uma decisão consciente, não esquecimento — e o item
> "discrepância de cor" do `ONBOARDING.md` §6 deve ser reescrito para refletir isso. Bege não
> entra no app interno.

Na prática:

- **Verde = lugar (chrome).** Rail de navegação, sub-aba selecionada e botão Administração usam
  a família de tokens `--sst-chrome-*` (fundo, borda, tinta, ativo-fundo, ativo-tinta) — sempre
  preenchimento sólido. É "onde estou", nunca "isso está bem".
- **Vinho `#670000` = ação (marca).** Token `--sst-color-primary`. Aparece em botão primário, em
  links e no traço superior da aba de pilar — o único lugar em que a marca sela seleção (aprovado
  02/09). O código já usa `#670000` como `colorPrimary`; não há mais um `#7B1E2B` sistemático — o
  levantamento da spec encontrou só um hex solto residual em `TrabalhadorDetalhePage.tsx:218`
  (ver seção "Correções ao levantamento inicial" da spec).
- **Bege `#ebe9ad` não entra no app interno.**

Ou seja: a "discrepância de cor" real deste projeto não é `#670000` vs. `#7B1E2B` — isso estava
desatualizado. A discrepância real, e deliberada, é **verde de chrome vs. a regra de cor da
organização**, registrada como escolha do usuário.

## 2. Tipografia: Montserrat

Confirmado no código (`src/AAHBRANT.SST.TeamsApp/src/index.css`, linhas 108 e 118):

```css
font-family: 'Montserrat', -apple-system, BlinkMacSystemFont, sans-serif;
font-family: 'Montserrat', sans-serif;
```

**Não é Inter nem Poppins** — essa era a informação do documento antigo (`DESING SYSTEM
AAHBRANT.md`) e estava errada; a spec já registra a correção ("Montserrat fica — já em produção;
o doc antigo dizia Inter/Poppins e estava errado").

Seis passos de escala, altura de linha fixa (`src/ui/tokens/tipografia.ts`, classes Griffel via
`useTipografia()`):

| Passo | px / peso / altura de linha | Uso |
|---|---|---|
| `display` | 28 / 800 / 32, `tabular-nums` | valor de KPI |
| `titulo` | 20 / 700 / 26 | título de página (substitui `Text size={500}`) |
| `subtitulo` | 16 / 600 / 22 | título de card e seção |
| `corpo` | 14 / 500 / 20 | padrão (= `fontSizeBase300` do Fluent) |
| `legenda` | 12 / 600 / 16 | rótulos, meta, cabeçalho de tabela |
| `micro` | 11 / 700 / 14, caixa alta, `letter-spacing .05em` | chips, sobretítulos |

Morreram os tamanhos soltos 10.5, 11.5, 12.5, 13, 15 e 26px que existiam antes da migração.

## 3. Famílias de tokens

### 3.1 Cor — três famílias, três papéis (spec §1.1)

- **Chrome** (`--sst-chrome-{fundo,borda,tinta,ativo-fundo,ativo-tinta}`) — verde, navegação,
  sempre sólido.
- **Marca** (`--sst-color-primary` = `#670000`) — ação: botão primário, links, traço de aba.
- **Status** (`--sst-status-{ok,atencao,alerta,info,neutro}-{tinta,fundo}`) — estado de um dado.
  Sempre fundo lavado + tinta + ícone, **nunca** sólido (única exceção declarada: o botão
  "destrutivo" do `ConfirmDialog`, que é ação, não estado). Um sistema só de status: `StatusChip`.
  `Badge color=` do Fluent é proibido em páginas.
- Modo escuro via `[data-theme]` em `index.css`/`ThemeModeContext.tsx`, sem mudança de mecanismo —
  os tokens novos só ganham valores nos dois blocos.

### 3.2 Tipografia (spec §1.2)

Ver seção 2 acima.

### 3.3 Espaçamento — grade de 4 (spec §1.3)

`xs 4 · sm 8 · md 12 · lg 16 · xl 24 · 2xl 32 · 3xl 48` (`tokensUi.espaco`). Morreram os valores
soltos 2, 6, 10, 14, 18, 20, 28. Regras de uso: padding de página `xl`; padding de card `xl`
(`lg` na variante densa); gap entre cards `lg`; gap interno de formulário `md`; gaps inline `sm`.

### 3.4 Raio e elevação (spec §1.4)

`sm 6px` (inputs, chips pequenos) · `md 10px` (botões, linhas de tabela, itens de navegação) ·
`lg 12px` (cards, diálogos, gavetas) · `full 999px` (chips, barra de progresso). Morreram 8, 9,
16, 20. Um card só: raio `lg`, padding `xl`, sombra `--sst-card-shadow` — é o componente `Card`.

### 3.5 Movimento — três durações, uma curva, um princípio (spec §1.5)

`rapido 120ms` (hover, toggle) · `normal 200ms` (troca de aba, chip, expandir linha) ·
`entrada 300ms` (montagem de página/card, gaveta). Curva única:
`cubic-bezier(0.2, 0, 0, 1)`. `src/ui/tokens/movimento.ts` exporta os presets framer-motion
`entrada`, `escalonado(indice)` e `deslizarDe('direita' | 'baixo')` — nenhuma página escreve
`transition={{ duration }}` à mão. Princípio: o movimento mostra **origem** (a gaveta desliza da
borda por onde entrou) ou **chegada** (cards sobem com fade, escalonados 40ms), nunca decoração.
`prefers-reduced-motion: reduce` zera todas as durações.

### 3.6 Quem usa o quê (spec §1.6)

- `src/ui/*` pode usar `designTokens` (semântica do app) e `tokens` do Fluent (só para estilizar
  internals do Fluent).
- **Páginas não usam nenhum dos dois diretamente** — compõem componentes de `src/ui/`. O lint
  garante isso (ver seção 4.2 abaixo).
- Hex cru: zero fora de `index.css` e `theme.ts`.

## 4. Arquitetura de `src/ui/`

### 4.1 Estrutura

```
src/ui/
  tokens/       tokens.ts · tipografia.ts · movimento.ts · tons.ts
  primitivos/   wrapper fino de 1 componente Fluent (ou sem Fluent)
  compostos/    combinam vários primitivos
  layout/       DetailPageLayout · WorkflowActions
  graficos/     paleta.ts + re-export dos 4 charts Recharts
  galeria/      GaleriaPage/Secao — a rota de documentação viva (/#/ui-galeria, só em DEV)
  index.ts      única porta de saída; também re-exporta os primitivos Fluent permitidos
```

### 4.2 Regra de dependência (spec §2.2)

- `ui/tokens` → nada do app (só `tokens` do Fluent e framer-motion).
- `ui/primitivos`, `compostos`, `layout`, `graficos` → Fluent + `ui/tokens` + outros `ui/*`.
  **Nunca** `lib/api`, `pages/`, `teams/`. Componentes recebem dados prontos por props; quem busca
  dado é a página.
- **`pages/` → `@ui`, `lib/`, React Router. Nunca `@fluentui/react-components` direto.**
- Exceção declarada: `Avatar`, `Button`, `Checkbox`, `Field`, `Input`, `Select`, `Spinner`,
  `Text`, `Textarea`, `Tooltip` são primitivos Fluent que já estavam certos e não ganham wrapper
  — são re-exportados por `ui/index.ts`. `Radio`/`RadioGroup` entraram nessa mesma lista de
  exceção na Onda 2 Task 18 (achado: `QuestionarioAplicabilidadeTab.tsx`, único consumidor no app
  até então) — não estavam na lista original da spec §2.2 por não terem uso.
- Exceção pontual adicional (spec §5.1, achado de Onda 2): `Table`/`TableBody`/`TableCell`/
  `TableHeader`/`TableHeaderCell`/`TableRow` crus continuam re-exportados por `ui/index.ts` **só**
  para grades que genuinamente não são lista de dados (`MatrizRiscoTab.tsx` — heatmap
  Probabilidade × Severidade com `<Select>` em cada célula; `ControleAcessoTab.tsx` — matriz
  módulo × escopo com bulk-toggle por coluna). Para qualquer caso de lista, é sempre `DataTable`.

### 4.3 Gate de lint (spec §2.4)

`.oxlintrc.json` (em `src/AAHBRANT.SST.TeamsApp/.oxlintrc.json`) tem `no-restricted-imports` por
pasta: em `src/pages/**` proíbe `@fluentui/react-components` e `../theme`; em `src/ui/**` proíbe
`**/lib/api` e `**/pages/**`.

**Estado atual confirmado no código (nesta branch, a partir de `origin/master`):** a regra em
`src/pages/**` está em `"warn"`, não `"error"`. A Task 23 do plano da Onda 3 (que sobe esse nível
para `error`) **ainda não foi mesclada em `master`** — `git log origin/master --
src/AAHBRANT.SST.TeamsApp/.oxlintrc.json` mostra só o commit `5e96e71 chore(ui): gate de lint
no-restricted-imports em warn` como a mudança mais recente nesse arquivo. Não presumir `error`
até essa task ser confirmada mesclada.

### 4.4 Alias

`@ui` e `@ui/*` apontam para `src/ui/index.ts` e `src/ui/*` (`tsconfig.app.json` + `vite.config.ts`).

### 4.5 Nomenclatura

Português nos componentes novos; `StatusChip` e `KpiCard` mantêm o nome por já serem termos
correntes no código. Props em português (`aoFechar`, `carregando`, `tom`). Um componente por
arquivo, arquivo com o nome do componente.

## 5. Inventário de componentes

Contagem real confirmada nesta branch (não presumida do plano): **9 pastas em
`src/ui/primitivos/`**, **7 pastas em `src/ui/compostos/`** e **2 pastas em `src/ui/layout/`** —
18 pastas de componente. A pasta `compostos/Formulario/` exporta três peças (`FormSection`,
`FormRodape`, `FormGrid`/`Campo`), então o total de peças próprias exportadas por essas 18 pastas
é 20. Além delas, `src/ui/graficos/` reexporta 4 gráficos que **ainda não foram fisicamente
movidos** para dentro de `ui/` (continuam em `src/components/dashboard/charts/`, por comentário
explícito no próprio `graficos/index.ts`: "até a Onda 3"), e `ui/index.ts` também reexporta
`CampoData`/`ChipsField`, que também ainda não migraram de `src/components/`. A frase de propósito
abaixo é copiada do comentário de cabeçalho de cada arquivo (convenção existente desde a Onda 0/1).

### Primitivos (`src/ui/primitivos/`)

| Componente | Quando usar |
|---|---|
| **StatusChip** | Chip de estado: sempre fundo lavado + tinta + ponto/ícone, nunca sólido — é o que o separa do verde de navegação. Substitui `useStatusChipStyles`, `BadgeVencimento`, `<Badge color=>` do Fluent e cores hex passadas por prop. `pulsar` é o único movimento espontâneo além da chegada. |
| **SeletorPesquisavel** | Seletor com busca por texto. Wrapper de `Combobox` do Fluent. Substitui `<Select>` quando a lista passa de ~15 itens — trabalhadores, EPIs, cursos, obras. Ignora acentos. |
| **ChipCheckboxGroup** | Seleção múltipla em chips clicáveis inteiros (pedido do usuário 02/09). Formaliza `useCheckboxChipStyles`, usado em matrizes, PT e DDS. |
| **ConfirmDialog** (hook `useConfirmar`) | Confirmação por promise. Absorve `useConfirmarExclusao` (39 telas) mantendo a mesma API — e corrige o botão Excluir, que usava `appearance="primary"` (vinho, cara de ação principal) para uma ação destrutiva. |
| **FeedbackInline** | Mensagem de feedback dentro da página. Wrapper de `MessageBar` do Fluent (0 usos antes da migração). Substitui `<Text className={estilos.erro}>` — erro sem ícone, sem ação e sem fechar. |
| **Carregando** | Skeleton no formato do conteúdo que substitui. Renomeia `components/ListaCarregando.tsx` e ganha variantes (`lista`, `card`, `kpi`, `detalhe`) — a diferença entre "carregando" e "vazio" foi pedido do usuário em 31/08. |
| **EstadoVazio** | Estado vazio com orientação: um vazio sem ação é porta fechada; com ação é convite. Substitui `components/EstadoVazio.tsx` (só uma linha cinza) e `pages/EmConstrucaoPage.tsx`. |
| **BarraProgresso** | Barra de progresso fina para uma fração/percentual dentro de um card. Achado na Onda 2 Task 2: `PerfilGeralTab` montava isto à mão com `designTokens` direto na página, e usava vinho `colorPrimary` — a marca é reservada para ação, nunca para dado. |
| **Legenda** | Texto secundário/meta — datas, contagens auxiliares, rótulos de campo. Achado na Onda 2 Task 2: páginas montavam isto com `<Text style={{ color: designTokens... }}>` — cor de token cru é privilégio de `src/ui/`, nunca de página. |

### Compostos (`src/ui/compostos/`)

| Componente | Quando usar |
|---|---|
| **PageHeader** | Cabeçalho de página: título + subtítulo à esquerda, filtros e ações à direita, link de voltar opcional acima. Substitui o par `toolbar` + `<Text size={500} weight="semibold">` repetido em toda página e o boolean `mostrarTitulo`. |
| **Abas** | Abas em três níveis (`pilar`, `modulo`, `interno`). Encapsula `TabList` com os estilos que viviam em `pageStyles` (`usePillTabStyles` → pilar, `useSubTabStyles` → módulo) e o terceiro nível, que não tinha estilo. |
| **DataTable** | Tabela do sistema: linhas-como-cartão, estados de carregando/vazio embutidos, densidade, ações por linha, linha expansível. Substitui os 63 usos de `<Table>` cru do Fluent. |
| **FormSection** (+ **FormRodape**) | Seção de formulário: rótulo pequeno em versalete que divide um formulário longo em blocos nomeados. Formaliza `usePageStyles.sectionTitle`/`sectionTitleFirst`. `FormRodape`: rodapé de formulário longo, texto de ajuda à esquerda, ações à direita — formaliza `usePageStyles.footer`. |
| **FormGrid** (+ **Campo**) | Grade de 12 colunas para formulários com larguras deliberadas. Formaliza `usePageStyles.formGrid` + `col2..col12`. Abaixo de 900px, cada `Campo` ocupa a única coluna disponível. |
| **KpiCard** | Cartão de indicador: valor grande + rótulo à esquerda, ícone em círculo lavado à direita, pílulas de variação. Promove `components/dashboard/KpiCard.tsx` trocando `cor: string` por `tom` — elimina os 41 hex soltos dos painéis. |
| **PainelLateral** | Painel que desliza da borda direita: formulários de criação saem de cima da tabela e vêm para aqui, com a lista visível atrás. Wrapper de `OverlayDrawer`. Absorve o estilo de `pages/pessoas/TrabalhadoresGaveta.tsx`. |
| **Card** | O card único do sistema: raio `lg`, padding `xl` (`lg` na densa), uma sombra por tema. Substitui `usePageStyles.card`, `useDashboardStyles.chartCard`/`motorPainel` e cards ad hoc. |

### Layout (`src/ui/layout/`)

| Componente | Quando usar |
|---|---|
| **DetailPageLayout** | Página de detalhe: cabeçalho com voltar/título/status/ações; lateral fixa à direita com resumo e ações do fluxo; conteúdo em seções à esquerda. Abaixo de 1100px a lateral sobe. É a forma das 6 páginas de detalhe de ~500 linhas (NC, PCMSO, DDS, Inspeção, Reunião CIPA, PT). |
| **WorkflowActions** | Ações do fluxo de um registro, extraídas de `NaoConformidadeDetalhePage`. A página passa só as ações permitidas no estado atual; cada uma abre o próprio formulário inline, uma por vez. Sem formulário, o clique executa direto. |

### Gráficos (`src/ui/graficos/`, ainda hospedados em `src/components/dashboard/charts/`)

Reexportados por `ui/graficos/index.ts` com o comentário "os quatro componentes ficam em
`src/components/dashboard/charts/` até a Onda 3 — aqui só expomos a porta `ui/`". Ainda não têm o
comentário de cabeçalho de propósito no padrão do resto de `src/ui/` — descrição abaixo inferida
das props e do nome, não copiada de um comentário (diferente das tabelas anteriores):

| Componente | Quando usar (inferido — sem comentário de cabeçalho no arquivo) |
|---|---|
| **RankingBarChart** | Ranking horizontal de itens por valor, com linha de referência opcional. |
| **StatusDonutChart** | Distribuição de um total em fatias de status (donut). |
| **TrendBarChart** | Série de barras ao longo do tempo. |
| **TrendLineChart** | Série de área/linha ao longo do tempo. |

`paleta.ts` (`usePaletaGraficos`) não é um componente — é o hook que lê `--sst-status-*`,
`--sst-chrome-ativo-fundo` e `--sst-color-primary` das CSS vars em tempo de execução, para os
gráficos Recharts seguirem o tema claro/escuro sozinhos.

### Peças antigas ainda não migradas para dentro de `ui/`

`CampoData` (`src/components/CampoData.tsx`) e `ChipsField` (`src/components/ChipsField.tsx`) já
são reexportadas por `ui/index.ts` (então páginas já importam de `@ui`), mas os arquivos em si
continuam fisicamente em `src/components/`, não em `src/ui/primitivos/`. `ChipsField` tem
comentário de propósito ("Campo de 'chips' removíveis para um campo de texto que guarda uma lista
curta como string única delimitada"); `CampoData` não tem.

### Fica fora, de propósito (spec §3, "Fica fora, de propósito")

Editor rico, upload genérico, calendário completo (`CalendarioPage` mantém layout próprio), edição
inline em célula de tabela, vista em grade real de matriz (função × item). Entram só se um piloto
pedir.

## 6. O que não se documenta aqui

Por decisão da spec (§7): não se cria wiki, Storybook nem ADRs separados para este sistema de
design. A galeria (`/#/ui-galeria`) é a documentação viva; a spec
`2026-09-07-sistema-de-design-design.md` é o histórico completo da decisão.

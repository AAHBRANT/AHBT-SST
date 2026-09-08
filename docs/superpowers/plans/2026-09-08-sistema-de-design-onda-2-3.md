# Sistema de Design — Onda 2 e 3 (Plano B) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrar as ~121 páginas restantes de `src/pages/` (que hoje estilizam à mão com `@fluentui/react-components` e `pageStyles.ts`) para compor a camada `src/ui/` — completando a varredura mecânica (Onda 2) e depois removendo todo o legado (Onda 3), até que `no-restricted-imports` vire `error` e nenhuma página importe Fluent direto.

**Architecture:** Nenhum componente novo nasce nesta frente — `src/ui/` já existe completo (Onda 0) e foi validado nos 3 pilotos (Onda 1). Este plano é puramente aplicativo: cada task troca, arquivo por arquivo, os padrões legados listados no "Guia de conversão" pelos componentes equivalentes de `@ui`, agrupando arquivos por módulo de página para manter revisão e contexto coerentes. A Onda 3, no fim, apaga o que a varredura deixou órfão.

**Tech Stack:** React 19, Fluent UI v9, Vite 8, `@ui` (`src/ui/`, alias já configurado), Playwright (`ui:snapshots`), oxlint.

**Spec:** `docs/superpowers/specs/2026-09-07-sistema-de-design-design.md` (seções 2, 4, 5.1, 7 e "Aprendizados dos pilotos (Onda 1)" — leitura obrigatória antes de qualquer task).

## Global Constraints

- Nenhum arquivo em `src/pages/` importa `@fluentui/react-components`, `../theme`, `../../theme` ou `../../../theme` diretamente ao final da Onda 2 (spec §2.4; critério de pronto #3) — só de `@ui`.
- `src/ui/**` nunca importa `lib/api`, `pages/**` (spec §2.2) — componentes recebem dados prontos por props.
- Zero hex fora de `index.css` e `theme.ts` (spec §1.6, critério de pronto #4).
- Português nos nomes de prop e de componente novo; `aoFechar`/`carregando`/`tom` como padrão já estabelecido (spec §2.5).
- **Regra de ouro do §5.1, reconfirmada pelos 3 pilotos da Onda 1 (nenhuma exceção até aqui): se uma peça de `src/ui/` não serve a um caso real, corrige-se a peça — nunca se contorna na página.** Toda task abaixo pode, em princípio, terminar achando um defeito de componente; se achar, o fix de componente entra num commit próprio, com galeria e snapshot atualizados, antes do commit da(s) página(s).
- Toda task roda, antes do commit: `npx tsc -b`, `npx oxlint <arquivos tocados>` (deve dar 0 `no-restricted-imports` nos arquivos migrados — está em `warn` até a Onda 3, mas o critério é tratado como gate desde já), `npx oxlint src/ui` (deve continuar vazio), `npx vite build --logLevel error`, `npm run ui:snapshots` (deve permanecer verde; regravar só se a mudança for em `src/ui/`, nunca por causa de uma página).
- A API .NET provavelmente não sobe nos worktrees (mesma causa documentada nos 3 pilotos — `appsettings.Development.json`/SQL ausentes). Verificação no navegador com `page.route` stub é aceitável; declarar "dados reais pendentes em homologação" no relatório de cada task, como os 3 pilotos já fizeram.
- 8+ worktrees de feature seguem ativas durante esta frente (mesma preocupação de coordenação da Onda 1, spec §5.3): feature já em andamento termina como começou e é migrada junto com o módulo dela; feature nova nasce em `@ui`.

---

## Correção ao levantamento da spec

A spec (§5.1) e o plano da Onda 0–1 estimam "~60 arquivos restantes com `<Table>`". Levantamento real
feito no início desta frente (`grep -rlE` sobre `src/pages/**/*.tsx`, união de 5 padrões — `<Table`,
`Badge…color=`, `useConfirmarExclusao`, `estilos.erro`/`estilosErro`, `Text size={500}`):

| Padrão | Arquivos |
|---|---|
| `<Table` cru | 60 |
| `Badge …color=` | 37 |
| `useConfirmarExclusao` | 40 |
| `estilos.erro`/`estilosErro` | 77 |
| `Text size={500}` | 36 |
| **União (arquivos únicos com pelo menos um)** | **121** |

`<Table>` continua sendo o item mais visível e o que dá mais trabalho por arquivo, mas **61 arquivos
adicionais** têm outro padrão legado sem ter tabela (páginas de detalhe pequenas, diálogos, painéis de
dashboard) — inclusive as próprias páginas-pilar e `*DashboardTab` (que usam `usePillTabStyles`/
`useSubTabStyles` de `pageStyles.ts`, sem bater em nenhum dos 5 grep acima, mas que precisam trocar
para `Abas`/`KpiCard` do mesmo jeito). O escopo real da Onda 2 é **121 arquivos em 21 módulos**, não 60.
Isso é registrado aqui pela mesma razão que a spec já registra outras correções ao levantamento
inicial: honestidade do histórico. Não muda a estratégia (ondas, lotes por módulo, ajustar peça não
página) — só o tamanho do trabalho de varredura.

---

## Guia de conversão

Referência única para todas as tasks de Onda 2. Cada task lista, por arquivo, **quais** destas
conversões se aplicam (nem todo arquivo precisa de todas); o "como" é sempre este.

### 1. `<Table>` cru → `DataTable` (template §4.2 "Lista")

Exemplo real, `src/pages/pgr/PgrsTab.tsx` (representativo do padrão repetido nos ~60 arquivos com
tabela: cabeçalho + `TableRow` clicável navegando para detalhe + coluna de ações com botões de
ícone):

**Antes:**
```tsx
{carregandoLista ? (
  <ListaCarregando />
) : pgrs.length === 0 ? (
  <EstadoVazio mensagem="Nenhum PGR cadastrado ainda." />
) : (
<Table noNativeElements>
  <TableHeader>
    <TableRow>
      <TableHeaderCell>Nome</TableHeaderCell>
      <TableHeaderCell>Obra</TableHeaderCell>
      <TableHeaderCell>Elaboração</TableHeaderCell>
      <TableHeaderCell>Próxima revisão</TableHeaderCell>
      <TableHeaderCell>Término</TableHeaderCell>
      <TableHeaderCell>Status</TableHeaderCell>
      <TableHeaderCell></TableHeaderCell>
    </TableRow>
  </TableHeader>
  <TableBody>
    {pgrs.map((pgr) => (
      <TableRow key={pgr.id} onClick={() => navigate(`/prevencao/pgr/${pgr.id}`)} style={{ cursor: 'pointer' }}>
        <TableCell>{pgr.nome}</TableCell>
        <TableCell>{nomeObra(pgr.obraId)}</TableCell>
        <TableCell>{pgr.dataElaboracao?.slice(0, 10)}</TableCell>
        <TableCell>{pgr.dataProximaRevisao?.slice(0, 10)}</TableCell>
        <TableCell>{pgr.dataTermino?.slice(0, 10)}</TableCell>
        <TableCell>{statusPgrLabel[pgr.status]}</TableCell>
        <TableCell>
          <div style={{ display: 'flex', gap: 4 }}>
            <Button appearance="subtle" icon={<ChevronRight24Regular />} onClick={() => navigate(`/prevencao/pgr/${pgr.id}`)} aria-label="Ver PGR" />
            <Button appearance="subtle" icon={<Delete24Regular />} onClick={(evento) => excluir(pgr.id, evento)} aria-label="Excluir" />
          </div>
        </TableCell>
      </TableRow>
    ))}
  </TableBody>
</Table>
)}
```

**Depois:**
```tsx
import { Button, Card, DataTable, EstadoVazio, type Coluna } from '@ui';

const colunas: Coluna<Pgr>[] = [
  { chave: 'nome', rotulo: 'Nome' },
  { chave: 'obra', rotulo: 'Obra', render: (p) => nomeObra(p.obraId) },
  { chave: 'elaboracao', rotulo: 'Elaboração', render: (p) => p.dataElaboracao?.slice(0, 10) ?? '' },
  { chave: 'revisao', rotulo: 'Próxima revisão', render: (p) => p.dataProximaRevisao?.slice(0, 10) ?? '' },
  { chave: 'termino', rotulo: 'Término', render: (p) => p.dataTermino?.slice(0, 10) ?? '' },
  { chave: 'status', rotulo: 'Status', render: (p) => statusPgrLabel[p.status] },
];

<Card>
  <DataTable
    aria-label="PGRs cadastrados"
    colunas={colunas}
    linhas={pgrs}
    chaveLinha={(p) => p.id}
    carregando={carregandoLista}
    vazio={{ titulo: 'Nenhum PGR cadastrado ainda.' }}
    aoClicarLinha={(p) => navigate(`/prevencao/pgr/${p.id}`)}
    acoesLinha={(p) => (
      <Button appearance="subtle" icon={<Delete24Regular />} onClick={(evento) => { evento.stopPropagation(); excluir(p.id, evento); }} aria-label="Excluir" />
    )}
  />
</Card>
```

Regras da conversão: a linha inteira já é clicável via `aoClicarLinha` — o botão "ver" que só repetia
a navegação da linha (`ChevronRight24Regular`) **sai**, ele era redundante mesmo antes da migração.
`acoesLinha` chama `evento.stopPropagation()` (o `DataTable` já faz isso por padrão nas ações, mas o
handler de exclusão em si costuma esperar o evento — conferir caso a caso). `EstadoVazio` deixa de ser
`mensagem` (API antiga de `components/EstadoVazio.tsx`) e vira `{ titulo, descricao?, acao? }` (API de
`@ui`, spec §3) — **oportunidade de adicionar `acao` quando o destino de cadastro é óbvio** (aprendizado
do piloto 1 e ruling da Onda 1: "um vazio sem ação é porta fechada").

### 2. `estilos.card` + `estilos.toolbar` → `Card` + `PageHeader`

**Antes:**
```tsx
<div className={estilos.card}>
  <div className={estilos.toolbar}>
    <Text weight="semibold">Programas de Gerenciamento de Riscos (PGR)</Text>
  </div>
  {erro && <Text className={estilos.erro}>{erro}</Text>}
  {/* ... */}
</div>
```

**Depois:**
```tsx
<PageHeader titulo="Programas de Gerenciamento de Riscos (PGR)" />
{erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}
<Card>
  {/* ... */}
</Card>
```

`PageHeader` fica **fora** do `Card` (é cabeçalho de página, não de cartão — ver template §4.2).
Quando a página tinha uma ação principal no canto (ex.: um botão "+ Novo" que hoje fica solto acima da
tabela), ela vira `acoes` do `PageHeader`. Se o formulário de criação está na mesma tela (padrão antigo,
empurrando a lista pra baixo — ver Onda 2, nota de `PainelLateral` abaixo), ele sai para um
`PainelLateral`, igual ao piloto 1.

### 3. `Text size={500} weight="semibold"` → `PageHeader.titulo` / `Text weight="semibold"` avulso → `subtitulo`/`Card.titulo`

Já coberto no exemplo acima quando é o título de página. Quando é um título de card ou seção interna
(não de página inteira), vira `<Card titulo="...">` ou `<FormSection titulo="...">`, não `PageHeader`
— um `PageHeader` por página, nunca mais de um.

### 4. `<Text className={estilos.erro}>{erro}</Text>` → `FeedbackInline`

**Antes:** `{erro && <Text className={estilos.erro}>{erro}</Text>}`

**Depois:** `{erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}`

`aoFechar` é novo — toda conversão ganha o botão de fechar (o componente já suporta; a página antiga
não tinha jeito de descartar o erro manualmente). Se a página tem MAIS de um estado de erro (ex.: erro
de carga da lista + erro de um formulário dentro de um painel), aplicar o aprendizado da Onda 1: **erro
de painel é estado PRÓPRIO, renderizado como primeiro filho do `PainelLateral`** — nunca o mesmo
`erro` de nível de página.

### 5. `Badge color={...} appearance="tint">{texto}</Badge>` → `StatusChip`

**Antes** (`src/pages/pgr/InventarioTab.tsx`):
```tsx
const corBadgeNivel: Record<number, 'success' | 'informative' | 'warning' | 'severe' | 'danger'> = {
  1: 'success', 2: 'informative', 3: 'warning', 4: 'severe', 5: 'danger',
};
// ...
<Badge color={corBadgeNivel[risco.nivelRisco]} appearance="tint">{rotuloNivel(risco.nivelRisco)}</Badge>
```

**Depois:**
```tsx
const tomPorNivel: Record<number, 'ok' | 'info' | 'atencao' | 'alerta'> = {
  1: 'ok', 2: 'info', 3: 'atencao', 4: 'alerta', 5: 'alerta',
};
// ...
<StatusChip tom={tomPorNivel[risco.nivelRisco]}>{rotuloNivel(risco.nivelRisco)}</StatusChip>
```

A união de tons do Fluent (`success/informative/warning/severe/danger`, 5 valores) não bate 1:1 com a
união de `StatusChip` (`ok/atencao/alerta/info/neutro`, 5 valores, mas semânticas diferentes) — mapear
com julgamento por caso, não mecanicamente por posição. **Onde o Badge já usava a regra dos 30 dias de
vencimento** (a maioria dos casos de `Badge` em páginas de trabalhador/documento), usar direto o helper
`nivelVencimento(data)` + `tomDeVencimento(nivel)`/`rotuloDeVencimento(nivel)` já exportados de `@ui`
em vez de reconstruir o mapeamento à mão — são os mesmos usados nos 3 pilotos.
**Aprendizado da Onda 1, reforçar aqui: `StatusChip` nunca substitui sozinho um valor de auditoria**
(data crua, contagem, número) que a coluna já mostrava — manter o valor cru ao lado do chip quando
havia um.

### 6. `useConfirmarExclusao()` → `useConfirmar()`

**Antes:**
```tsx
import { useConfirmarExclusao } from '../../hooks/useConfirmarExclusao';
// ...
const { confirmar, dialogElement } = useConfirmarExclusao();
// ...
if (!(await confirmar('Excluir este PGR? Essa ação não pode ser desfeita.'))) return;
```

**Depois:**
```tsx
import { useConfirmar } from '@ui';
// ...
const { confirmar, dialogElement } = useConfirmar();
// ...
if (!(await confirmar('Excluir este PGR? Essa ação não pode ser desfeita.'))) return;
```

API idêntica por design (spec §3: "`useConfirmar()` mantém a API atual") — é troca de import, não de
chamada. Confirmar que `{dialogElement}` continua renderizado uma vez no JSX da página (geralmente logo
dentro da `<div>` raiz).

### 7. `usePillTabStyles`/`useSubTabStyles` (página-pilar e `*DashboardTab`) → `Abas`

Páginas-pilar (`OperacaoPage.tsx`, `GestaoSstPage.tsx` e as demais) e as `*DashboardTab` usam
`usePillTabStyles`/`useSubTabStyles` de `pageStyles.ts` para estilizar um `TabList` do Fluent à mão —
não aparecem nos 5 grep do levantamento porque não usam `<Table>`/`Badge`/etc., mas ainda importam
`pageStyles`. Seguem o mesmo padrão já aplicado em `EpiPage.tsx` no piloto 1 (`aba` sincronizada com
`?secao=`/`?aba=` via `Abas` + `useAbaNaUrl`):

**Antes** (padrão em toda página-pilar hoje):
```tsx
const estilosAba = usePillTabStyles();
// ...
<TabList selectedValue={secao} onTabSelect={(_, d) => setSecao(d.value as Secao)} className={estilosAba.lista}>
  <Tab value="pgr">PGR e GRO</Tab>
  <Tab value="pcmso">PCMSO</Tab>
</TabList>
```

**Depois:**
```tsx
import { Abas, useAbaNaUrl } from '@ui';
// ...
const [secao, setSecao] = useAbaNaUrl<Secao>('secao', SECOES_VALIDAS, 'pgr');
// ...
<Abas
  nivel="pilar"
  abas={[{ valor: 'pgr', rotulo: 'PGR e GRO' }, { valor: 'pcmso', rotulo: 'PCMSO' }]}
  valor={secao}
  aoMudar={setSecao}
/>
```

**Correção a este guia (achado ao migrar Task 11, 2026-09-08):** `Abas` NÃO tem prop `param` — a
sincronização com a URL é feita pelo hook `useAbaNaUrl(param, valoresValidos, padrao)`, que devolve o
par `[valor, aoMudar]` a passar para `Abas`. `param="secao"` direto no `<Abas>` não compila. Ver o uso
real em `src/pages/epi/EpiPage.tsx` (piloto 1) — é a referência que este guia deveria ter citado.
`param="aba"` no nível `modulo` vem do mesmo hook, só trocando a primeira string.

### 8. Dashboard (`KpiCard` + `Card` de gráfico)

`*DashboardTab.tsx` usa hoje uma grade de `<div className={estilos.kpiCard} style={{borderLeft: cor}}>`
com hex cru — vira `KpiCard` com `tom` em vez de `cor`, dentro de uma grade CSS Grid simples
(`repeat(auto-fit, minmax(185px, 1fr))`, já documentada na spec §4.4 — não precisa de componente de
grade próprio). Gráficos (`RankingBarChart`/`StatusDonutChart`/`TrendBarChart`/`TrendLineChart`, já em
`ui/graficos`) entram dentro de `<Card titulo="...">`.

---

## Onda 2 — Varredura mecânica (21 tasks, ~5–9 arquivos por task, agrupadas por módulo)

Cada task segue a mesma estrutura de passos — só a lista de arquivos e as conversões aplicáveis
mudam. Ela é descrita uma vez aqui e referenciada pelo número dos passos em cada task, para não repetir
90 vezes o mesmo texto:

> **Passos padrão de toda task de Onda 2:**
> 1. Ler cada arquivo listado; aplicar do Guia de conversão as seções indicadas na tabela da task.
> 2. Se algum componente de `@ui` não cobrir um caso do arquivo, corrigir o componente **primeiro**,
>    num commit próprio, com galeria + snapshot atualizados (regra §5.1) — só depois seguir para os
>    arquivos de página.
> 3. Rodar: `npx tsc -b && npx oxlint <arquivos tocados> && npx oxlint src/ui && npx vite build --logLevel error && npm run ui:snapshots`.
> 4. Verificar cada tela alterada no navegador (dev server), nos dois temas — dados reais se a API
>    subir no ambiente, senão estados de casca/vazio/carregando/erro com stub, declarando a pendência.
> 5. Commit(s) — um por arquivo-página ou por pequeno grupo coeso do mesmo submódulo, mensagem
>    `refactor(<módulo>): <ArquivoTab> na camada ui/`, trailer `Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>`.
> 6. Push da branch da task + `gh pr create` com resumo de quais conversões (1–8) foram aplicadas em
>    quais arquivos e o que foi verificado.

### Task 1: Módulo `pessoas` — Cadastros e listas

**Files:**
- Modify: `src/pages/pessoas/FuncoesTab.tsx` (conversões 1, 2, 4, 6)
- Modify: `src/pages/pessoas/SetoresTab.tsx` (1, 2, 4, 6)
- Modify: `src/pages/pessoas/EquipesTab.tsx` (1, 2, 4, 6)
- Modify: `src/pages/pessoas/RiscosTab.tsx` (1, 2, 5)
- Modify: `src/pages/pessoas/CursosTreinamentoTab.tsx` (1, 2, 4, 6)
- Modify: `src/pages/pessoas/TreinamentosTab.tsx` (1, 2, 4, 5, 6)
- Modify: `src/pages/pessoas/MatrizTreinamentoTab.tsx` (1 — variante `expansivel`, template §4.5, mesmo padrão de `MatrizEpiTab`; 2, 4)
- Modify: `src/pages/pessoas/OcorrenciasTab.tsx` (1, 5)

**Interfaces:** nenhuma nova — consome `@ui` (`Card`, `PageHeader`, `DataTable`, `StatusChip`,
`FeedbackInline`, `useConfirmar`, `Coluna`) e `lib/api` (inalterado).

- [ ] Passos padrão 1–6 acima. Para `MatrizTreinamentoTab.tsx`, usar `MatrizEpiTab.tsx` (já migrado,
  piloto 3, com o chevron ▸/▾) como referência direta em vez do exemplo genérico do Guia — é o mesmo
  template, mesmas armadilhas já resolvidas (dirty state ao trocar de linha, `EstadoVazio` com ação).

### Task 2: Módulo `pessoas` — Trabalhador (detalhe, gaveta, diálogos)

**Files:**
- Modify: `src/pages/pessoas/TrabalhadorDetalhePage.tsx` (2, 3, 4, 5)
- Modify: `src/pages/pessoas/TrabalhadoresTab.tsx` (1, 4, 6)
- Modify: `src/pages/pessoas/TrabalhadoresGaveta.tsx` (5 — vira consumidor de `PainelLateral`, já
  existente em `@ui`; hoje é o componente que a spec §3 cita como o estilo que `PainelLateral` absorve)
- Modify: `src/pages/pessoas/AssinaturaTab.tsx` (4)
- Modify: `src/pages/pessoas/PerfilGeralTab.tsx` (5)
- Modify: `src/pages/pessoas/RequisitosFuncaoDialog.tsx` (4)
- Modify: `src/pages/pessoas/CadastroDigitalDialog.tsx` (4)
- Modify: `src/pages/pessoas/EntregasEpiTab.tsx` (1, 5, 4 — histórico somente-leitura apontando para
  `MatrizEpiTab`, ver comentário já existente no arquivo)
- Modify: `src/pages/pessoas/CofreAssinaturasTab.tsx` (1, 4)

**Interfaces:** `TrabalhadoresGaveta` passa a usar `PainelLateral` de `@ui` (`aberto`, `aoFechar`,
`titulo`, `largura`) — conferir que os consumidores de `TrabalhadoresGaveta` (é usada por outras
telas) continuam recebendo as mesmas props públicas do componente; só o interior muda.

- [ ] Passos padrão 1–6. `TrabalhadorDetalhePage.tsx` é a maior peça deste grupo — se tiver lateral
  fixa de resumo/ações, avaliar se cabe no template §4.3 (`DetailPageLayout`); se for mais parecida
  com uma ficha de perfil sem workflow de estados, manter como `PageHeader` + `Card`s empilhados
  (não forçar o template errado).

### Task 3: Módulo `pessoas` — Página-pilar e dashboard

**Files:**
- Modify: `src/pages/pessoas/PessoasPage.tsx` (3, 7)
- Modify: `src/pages/pessoas/dashboard/PessoasDashboardTab.tsx` (8)
- Modify: `src/pages/pessoas/dashboard/AptitudeEnginePanel.tsx` (5, 8)

- [ ] Passos padrão 1–6.

### Task 4: Módulo `cipa` — Listas e tabs

**Files:**
- Modify: `src/pages/cipa/DimensionamentoCipaTab.tsx` (1, 4, 6)
- Modify: `src/pages/cipa/InspecoesCipaTab.tsx` (1, 4, 5, 6)
- Modify: `src/pages/cipa/MembrosCipaTab.tsx` (1, 4, 5)
- Modify: `src/pages/cipa/ProcessoEleitoralCipaTab.tsx` (1, 4, 6)
- Modify: `src/pages/cipa/ReunioesCipaTab.tsx` (1, 4, 6)
- Modify: `src/pages/cipa/SipatTab.tsx` (1, 4, 6)

- [ ] Passos padrão 1–6.

### Task 5: Módulo `cipa` — Detalhes e página-pilar

**Files:**
- Modify: `src/pages/cipa/EventoSipatDetalhePage.tsx` (1, 3, 4)
- Modify: `src/pages/cipa/MembroCipaDetalhePage.tsx` (1, 3, 4, 5, 6)
- Modify: `src/pages/cipa/ProcessoEleitoralCipaDetalhePage.tsx` (1, 3, 4)
- Modify: `src/pages/cipa/ReuniaoCipaDetalhePage.tsx` (1, 3, 4, 6)
- Modify: `src/pages/cipa/CipaPage.tsx` (3, 7)

**Interfaces:** avaliar cada `*DetalhePage.tsx` deste grupo contra o template §4.3
(`DetailPageLayout`) — se tiver ações de fluxo por estado (workflow), usar `WorkflowActions` como em
`NaoConformidadeDetalhePage`; se for só ficha de leitura + edição, `PageHeader` + `FormSection`s basta.

- [ ] Passos padrão 1–6.

### Task 6: Módulo `pt` — Listas

**Files:**
- Modify: `src/pages/pt/PermissoesTrabalhoTab.tsx` (1, 4, 5, 6)
- Modify: `src/pages/pt/PreRequisitosPtTab.tsx` (1, 4)
- Modify: `src/pages/pt/RiscosCriticosPtTab.tsx` (1, 4, 6)
- Modify: `src/pages/pt/VerificacoesPtTab.tsx` (1, 4, 5)
- Modify: `src/pages/pt/EpiEpcPtTab.tsx` (4)
- Modify: `src/pages/pt/TiposTrabalhoPtTab.tsx` (4)

- [ ] Passos padrão 1–6.

### Task 7: Módulo `pt` — Detalhe, assinatura, página-pilar e dashboard

**Files:**
- Modify: `src/pages/pt/PermissaoTrabalhoDetalhePage.tsx` (2, 3, 4 — candidata a `DetailPageLayout` +
  `WorkflowActions`, é workflow de PT com estados)
- Modify: `src/pages/pt/AssinarPtPage.tsx` (2, 4)
- Modify: `src/pages/pt/PermissoesTrabalhoPage.tsx` (3, 7)
- Modify: `src/pages/pt/dashboard/PtDashboardTab.tsx` (8)
- Modify: `src/pages/pt/dashboard/PtVencidaPanel.tsx` (5, 8)

- [ ] Passos padrão 1–6.

### Task 8: Módulo `pgr`

**Files:**
- Modify: `src/pages/pgr/InventarioTab.tsx` (1, 5 — é o exemplo usado no Guia para `Badge→StatusChip`)
- Modify: `src/pages/pgr/PgrRevisoesTab.tsx` (1, 4)
- Modify: `src/pages/pgr/PgrsTab.tsx` (1, 2, 4, 6 — é o exemplo usado no Guia para `Table→DataTable`;
  aplicar literalmente a conversão já escrita lá)
- Modify: `src/pages/pgr/PlanoAcaoTab.tsx` (1, 4, 6)
- Modify: `src/pages/pgr/PgrDetalhePage.tsx` (2, 3, 4 — candidata a `DetailPageLayout`)
- Modify: `src/pages/pgr/PgrRiscosPage.tsx` (3)
- Modify: `src/pages/pgr/dashboard/PgrDashboardTab.tsx` (8)
- Modify: `src/pages/pgr/dashboard/PgrPlanoAcaoVencidoPanel.tsx` (5, 8)

- [ ] Passos padrão 1–6.

### Task 9: Módulo `identificacao`

**Files:**
- Modify: `src/pages/identificacao/AreasSstTab.tsx` (1, 4, 6)
- Modify: `src/pages/identificacao/TagsIdentificacaoTab.tsx` (1, 4, 6)
- Modify: `src/pages/identificacao/IdentificacaoPage.tsx` (3, 7)
- Modify: `src/pages/identificacao/IdentificacaoPublicaPage.tsx` (5, 3 — template §4.6 "Público": sem
  rail, tema claro forçado, `Card` centralizado, `Avatar` grande; conferir contra o exemplo do piloto
  já usado como referência de "Público" na spec)
- Modify: `src/pages/identificacao/LeitorNfcTab.tsx` (4)
- Modify: `src/pages/identificacao/dashboard/IdentificacaoDashboardTab.tsx` (8)
- Modify: `src/pages/identificacao/dashboard/AreasBloqueadasPanel.tsx` (5, 8)
- Modify: `src/pages/identificacao/dashboard/TagsPerdidasPanel.tsx` (5, 8)

- [ ] Passos padrão 1–6.

### Task 10: Módulo `inspecoes`

**Files:**
- Modify: `src/pages/inspecoes/ChecklistModelosTab.tsx` (1, 4, 6)
- Modify: `src/pages/inspecoes/InspecoesTab.tsx` (1, 4, 5)
- Modify: `src/pages/inspecoes/InspecaoDetalhePage.tsx` (2, 3, 4, 5 — candidata a `DetailPageLayout` +
  `WorkflowActions`)
- Modify: `src/pages/inspecoes/AssinarInspecaoPage.tsx` (2, 4)
- Modify: `src/pages/inspecoes/InspecoesPage.tsx` (3, 7)
- Modify: `src/pages/inspecoes/dashboard/InspecoesDashboardTab.tsx` (8)
- Modify: `src/pages/inspecoes/dashboard/InspecoesNaoConformesPanel.tsx` (5, 8)

- [ ] Passos padrão 1–6.

### Task 11: Módulo `apr`

**Files:**
- Modify: `src/pages/apr/AprAssinaturasTab.tsx` (1, 4)
- Modify: `src/pages/apr/AprEtapasTab.tsx` (1, 4, 6)
- Modify: `src/pages/apr/AprsTab.tsx` (1, 4, 5, 6)
- Modify: `src/pages/apr/AprDetalhePage.tsx` (2, 3, 4 — candidata a `DetailPageLayout`)
- Modify: `src/pages/apr/AprsPage.tsx` (3, 7)
- Modify: `src/pages/apr/dashboard/AprDashboardTab.tsx` (8)
- Modify: `src/pages/apr/dashboard/AprVencidaPanel.tsx` (5, 8)

- [ ] Passos padrão 1–6.

### Task 12: Módulo `saude-ocupacional`

**Files:**
- Modify: `src/pages/saude-ocupacional/AptidoesTab.tsx` (1, 4, 5, 6)
- Modify: `src/pages/saude-ocupacional/AsosTab.tsx` (1, 4, 5, 6)
- Modify: `src/pages/saude-ocupacional/ExamesComplementaresTab.tsx` (1, 4, 6)
- Modify: `src/pages/saude-ocupacional/PcmsoDetalhePage.tsx` (1, 2, 3, 4, 5, 6 — a página mais densa
  deste módulo; candidata a `DetailPageLayout`)
- Modify: `src/pages/saude-ocupacional/PcmsoTab.tsx` (1, 4, 6)
- Modify: `src/pages/saude-ocupacional/SaudeOcupacionalPage.tsx` (3, 7)

- [ ] Passos padrão 1–6.

### Task 13: Módulo `riscos`

**Files:**
- Modify: `src/pages/riscos/AtividadesTab.tsx` (1, 4, 6)
- Modify: `src/pages/riscos/ImportarLoteTab.tsx` (4, 6)
- Modify: `src/pages/riscos/MatrizRiscoTab.tsx` (1 — template §4.5, mesma referência de
  `MatrizEpiTab.tsx`; 4, 6)
- Modify: `src/pages/riscos/dashboard/ListaRiscosPanel.tsx` (1, 5, 6, 8)
- Modify: `src/pages/riscos/dashboard/RiscosCriticosPanel.tsx` (5, 8)
- Modify: `src/pages/riscos/dashboard/RiscosDashboardTab.tsx` (8)

- [ ] Passos padrão 1–6.

### Task 14: Módulo `dds`

**Files:**
- Modify: `src/pages/dds/AssinarDdsPage.tsx` (2, 4)
- Modify: `src/pages/dds/CatalogoTemasDdsPage.tsx` (1, 4, 6)
- Modify: `src/pages/dds/DdsDetalhePage.tsx` (1, 2, 3, 4, 5 — candidata a `DetailPageLayout`)
- Modify: `src/pages/dds/DdsPage.tsx` (3, 7)
- Modify: `src/pages/dds/DdsSemanalDetalhePage.tsx` (2, 3, 4 — já usa `WorkflowActions` de `@ui` desde
  o piloto 2, conferir que o resto da página em volta também está migrado, não só o trecho de ações)
- Modify: `src/pages/dds/DdsSemanalPage.tsx` (1, 5, 3, 4)

- [ ] Passos padrão 1–6.

### Task 15: Módulo `naoconformidades`

**Files:**
- Modify: `src/pages/naoconformidades/NaoConformidadesPage.tsx` (3, 7)
- Modify: `src/pages/naoconformidades/NaoConformidadesTab.tsx` (1, 4, 6)
- Modify: `src/pages/naoconformidades/dashboard/NaoConformidadesCriticasPanel.tsx` (5, 8)
- Modify: `src/pages/naoconformidades/dashboard/NaoConformidadesDashboardTab.tsx` (8)

**Interfaces:** `NaoConformidadesTab.tsx` deve navegar para `NaoConformidadeDetalhePage.tsx`, já
migrada (piloto 2) — conferir que a navegação e o formato de `StatusChip` do status da NC ficam
visualmente coerentes entre lista e detalhe (mesmos tons).

- [ ] Passos padrão 1–6.

### Task 16: Módulo `alertas`

**Files:**
- Modify: `src/pages/alertas/AlertasConfiguracaoTab.tsx` (1, 4, 5, 6)
- Modify: `src/pages/alertas/AlertasListaTab.tsx` (1, 4, 5, 6)
- Modify: `src/pages/alertas/AlertasPage.tsx` (3, 7)
- Modify: `src/pages/alertas/dashboard/AlertasDashboardTab.tsx` (8)

- [ ] Passos padrão 1–6.

### Task 17: Módulo `administracao`

**Files:**
- Modify: `src/pages/administracao/AdministracaoPage.tsx` (3, 7)
- Modify: `src/pages/administracao/ControleAcessoTab.tsx` (1, 4, 5, 6 — já usa `ChevronDown20Regular`/
  `ChevronRight20Regular` para um disclosure próprio; considerar se cabe como `DataTable expansivel`
  em vez de árvore custom, sem forçar se não couber)
- Modify: `src/pages/administracao/PainelAssinaturasTab.tsx` (1, 4)
- Modify: `src/pages/administracao/TrilhaAuditoriaTab.tsx` (1, 4 — é o arquivo usado como referência
  real de `estilos.erro` no Guia)

- [ ] Passos padrão 1–6.

### Task 18: Módulo `requisitoslegais`

**Files:**
- Modify: `src/pages/requisitoslegais/QuestionarioAplicabilidadeTab.tsx` (1, 4, 6)
- Modify: `src/pages/requisitoslegais/RequisitosLegaisPage.tsx` (3, 7)
- Modify: `src/pages/requisitoslegais/RequisitosLegaisTab.tsx` (1, 4, 5, 6)

- [ ] Passos padrão 1–6.

### Task 19: Módulo `epi` — restante

**Files:**
- Modify: `src/pages/epi/AssinarEntregaEpiPage.tsx` (2, 4)
- Modify: `src/pages/epi/CatalogoTab.tsx` (1, 4, 6)
- Modify: `src/pages/epi/EstoqueTab.tsx` (1, 4)

**Interfaces:** `EntregasTab.tsx`, `MatrizEpiTab.tsx` e `EpiPage.tsx` já migradas (Onda 1) — só faltam
estas 3. `EpiEpcPtTab.tsx` (módulo `pt`, Task 6) referencia EPIs por função — conferir consistência de
rótulo/tom com `CatalogoTab.tsx` migrado.

- [ ] Passos padrão 1–6.

### Task 20: Módulo `acidentes`

**Files:**
- Modify: `src/pages/acidentes/AcidenteDetalhePage.tsx` (1, 2, 3, 4 — candidata a `DetailPageLayout`)
- Modify: `src/pages/acidentes/AcidentesPage.tsx` (1, 4)
- Modify: `src/pages/acidentes/HhtMensalTab.tsx` (1, 4, 6)

- [ ] Passos padrão 1–6.

### Task 21: Raiz e módulos pequenos restantes

**Files:**
- Modify: `src/pages/DashboardPage.tsx` (4 — dashboard geral, grade de `KpiCard`s + gráficos, seguir
  conversão 8 mesmo sem estar listado nas 5 flags)
- Modify: `src/pages/ObrasPage.tsx` (1, 4, 6)
- Modify: `src/pages/gestao-sst/GestaoSstPage.tsx` (7)
- Modify: `src/pages/operacao/OperacaoPage.tsx` (7 — já tem `EpiPage`/`MatrizEpiTab` migradas como
  filhas; só a casca da página-pilar falta)
- Modify: `src/pages/calendario/CalendarioPage.tsx` (4 — mantém layout próprio por decisão da spec
  §4, "fora dos templates"; só troca o `erro` solto por `FeedbackInline`, sem reestruturar o resto)
- Modify: `src/pages/ocorrencias/OcorrenciasPage.tsx` (7)
- Modify: `src/pages/ocorrencias/dashboard/OcorrenciasDashboardTab.tsx` (8)
- Modify: `src/pages/treinamentos/AssinarTreinamentoPage.tsx` (2, 4)
- Modify: `src/pages/treinamentos/TreinamentosPage.tsx` (3, 7)
- Delete: `src/pages/EmConstrucaoPage.tsx` — **não migrar, apagar**. A spec §3 já diz que `EstadoVazio
  variante="em-construcao"` absorve este componente; localizar os import-sites restantes (`grep -rn
  "EmConstrucaoPage" src/`) e trocar cada um por `<EstadoVazio variante="em-construcao" titulo="..."
  />` antes de apagar o arquivo.

- [ ] Passos padrão 1–6 (exceto `EmConstrucaoPage.tsx`, que segue a instrução acima em vez do padrão).

---

## Onda 3 — Remoção (5 tasks)

Só começa depois que a Task 21 estiver mesclada e um novo `grep -rl "@fluentui/react-components"
src/pages --include="*.tsx"` (e o equivalente para `../theme`) devolver **zero** arquivos.

### Task 22: Verificação de zero-legado antes de remover

**Files:** nenhum modificado — só verificação.

- [ ] **Step 1:** Rodar e confirmar saída vazia:
```bash
grep -rl "@fluentui/react-components" src/pages --include="*.tsx"
grep -rl "from '\.\./theme'\|from '\.\./\.\./theme'\|from '\.\./\.\./\.\./theme'" src/pages --include="*.tsx"
grep -rlE "Badge.*color=|useConfirmarExclusao|estilos\.erro|estilosErro|Text size=\{500\}" src/pages --include="*.tsx"
```
Se alguma dessas listas não estiver vazia, **parar** — significa que uma das 21 tasks da Onda 2 ficou
incompleta ou um arquivo novo entrou nesse meio-tempo (uma das 8+ worktrees terminando uma feature
antiga sem migrar, contra a regra §5.3). Resolver antes de prosseguir para as tasks seguintes.

- [ ] **Step 2:** `npx tsc -b && npx oxlint src/pages && npx oxlint src/ui && npx vite build --logLevel error && npm run ui:snapshots` — todos devem passar limpos.

### Task 23: Lint vira `error` + `.oxlintrc.json`

**Files:**
- Modify: `.oxlintrc.json`

- [ ] **Step 1:** Trocar o nível de `no-restricted-imports` de `warn` para `error` nas duas regras
  (páginas proibidas de importar Fluent/theme direto; `ui/**` proibido de importar `lib/api`/`pages/**`
  — spec §2.4).
- [ ] **Step 2:** `npx oxlint src/` (sem argumento de arquivo — o projeto inteiro) deve dar **0
  erros**. Se algo quebrar aqui que a Task 22 não pegou, é sinal de que a Task 22 rodou contra um
  estado do repositório que já mudou — refazer a verificação de zero-legado.
- [ ] **Step 3:** Commit `chore(lint): no-restricted-imports vira error (Onda 3)` +
  `Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>`.

### Task 24: Remoção do legado de `src/ui/` e `components/`/`hooks/`

**Files:**
- Modify: `src/index.css` — remover o hack `.fui-TableRow.fui-TableRow` (linha-como-cartão virou
  estilo interno do `DataTable` desde a Onda 0) e os tokens antigos `--sst-color-rail-*`,
  `--sst-color-admin-button-*` (substituídos por `--sst-chrome-*` desde a Onda 0 — conferir com
  `grep -rn "\-\-sst-color-rail\|\-\-sst-color-admin-button" src/` antes de apagar, para não quebrar
  nenhum uso residual).
- Delete: `src/components/EstadoVazio.tsx`, `src/components/CampoData.tsx` (se ainda existirem como
  arquivos próprios em vez de re-export — conferir; a Onda 0 pode já ter deixado só um re-export aqui,
  caso em que é o re-export que sai), `src/hooks/useConfirmarExclusao.tsx`,
  `src/components/dashboard/BadgeVencimento.tsx` (ou onde estiver).
- Modify: `src/theme.ts` — mover (não mais re-exportar) `designTokens` para `src/ui/tokens/tokens.ts`
  de vez; qualquer import restante de `../theme`/`../../theme` já deveria ter sumido na Task 22, então
  este arquivo passa a ser só o que o Fluent `FluentProvider` consome diretamente (tema base), sem
  ponte para páginas.
- Modify: `src/pages/pageStyles.ts` — remover `usePageStyles.card`, `toolbar`, `sectionTitle`,
  `sectionTitleFirst`, `formGrid`, `col2..col12`, `erro` (tudo absorvido por `Card`/`PageHeader`/
  `FormSection`/`FormGrid`/`FeedbackInline`); manter só o que sobrar de uso genuíno fora do escopo
  desta frente (conferir com `grep -rn "usePageStyles" src/pages` antes de decidir o que fica).

- [ ] **Step 1:** Para cada remoção acima, `grep -rn` o símbolo em `src/` inteiro primeiro — zero
  ocorrências restantes é pré-condição, não suposição.
- [ ] **Step 2:** Remover. `npx tsc -b` (pega qualquer import quebrado que o grep não tenha achado,
  ex. import de tipo sem uso de valor) `&& npx oxlint src/ && npx vite build --logLevel error`.
- [ ] **Step 3:** `npm run ui:snapshots` — nenhuma seção de galeria deveria mudar (o hack de
  `.fui-TableRow` já não afeta `DataTable`, que tem estilo próprio desde a Onda 0; se algo mudar
  visualmente aqui, é sinal de que algo em `src/ui/` ainda dependia do hack global, o que seria um
  defeito de peça a corrigir antes de prosseguir).
- [ ] **Step 4:** Commit `refactor: remove legado pós-migração (usePageStyles, hooks/components
  antigos, tokens --sst-color-rail-*)` + trailer.

### Task 25: Documentação (spec §7)

**Files:**
- Create: `docs/design-system.md` (reescrita de `DESING SYSTEM AAHBRANT.md`, que sai da raiz)
- Modify: `ONBOARDING.md` — §5 ganha a regra de dependência de `ui/` (spec §2.2) e o gate de lint
  (spec §2.4, agora `error`); §6 corrige "discrepância de cor": o código usa `#670000` para marca, a
  discrepância real registrada é o verde de chrome (decisão consciente, spec, seção "Registro
  explícito").
- Delete: `DESING SYSTEM AAHBRANT.md` (nome com erro, raiz do repo).

- [ ] **Step 1:** Escrever `docs/design-system.md`: verde como chrome (decisão consciente contra a
  regra `#670000`/preto/branco/`#ebe9ad` da organização, registrada como tal — não é esquecimento),
  Montserrat (não Inter/Poppins), as 3 famílias de token de cor (§1.1) + a escala de tipografia (§1.2)
  + espaçamento (§1.3) + raio/elevação (§1.4) + movimento (§1.5), os 18 componentes do inventário
  (§3) cada um com uma linha de "quando usar" (copiar a frase de propósito do comentário de cabeçalho
  de cada arquivo em `src/ui/` — já existem desde a Onda 0/1, spec §7 "Comentário de cabeçalho em cada
  componente").
- [ ] **Step 2:** Atualizar `ONBOARDING.md` §5/§6 conforme acima.
- [ ] **Step 3:** Apagar `DESING SYSTEM AAHBRANT.md`.
- [ ] **Step 4:** Commit `docs: reescreve design-system.md, corrige ONBOARDING §5/§6 (Onda 3)` +
  trailer.

### Task 26: Fechamento da frente

**Files:** nenhum modificado — só verificação e relatório final.

- [ ] **Step 1:** Conferir os 6 critérios de pronto da spec, agora todos:
  1. `src/ui/` com todas as peças visíveis na galeria nos dois temas — `npm run ui:snapshots` verde.
  2. Os 3 pilotos + toda a Onda 2 em `master`, verificados no navegador (e em homologação, se o
     usuário tiver feito o deploy manual — spec ONBOARDING §7 — declarar o que foi/não foi feito,
     nunca presumir).
  3. `grep -rl "@fluentui/react-components" src/pages` vazio, com lint `error` (Task 23) confirmando.
  4. `grep -rn "#[0-9a-fA-F]\{3,6\}" src/pages --include="*.tsx"` vazio (zero hex fora de
     `index.css`/`theme.ts` — conferir manualmente os achados, `#` também aparece em contextos que
     não são cor, como comentários).
  5. `npm run ui:snapshots` passa.
  6. `docs/design-system.md` e `ONBOARDING.md` atualizados (Task 25).
- [ ] **Step 2:** Escrever um relatório final da frente (arquivo `.superpowers/sdd/` do projeto, fora
  do git, seguindo a mesma convenção usada pela Onda 0/1) resumindo as 26 tasks, achados de componente
  por task, e qualquer item que tenha ficado deferido para uma frente futura.
  Este é o **único** artefato desta task fora de `docs/` ou `src/`.

---

## Auto-revisão do plano

**Cobertura da spec:** §1 (tokens/tipografia/espaço/raio/movimento) já entregue na Onda 0, consumido
mecanicamente por toda task de Onda 2 via os componentes de `@ui` — nenhuma task de Onda 2 toca token
diretamente. §2 (arquitetura/lint) → Tasks 22–24. §3 (inventário de componentes) → nenhum componente
novo nesta frente; ajustes pontuais só se uma task achar defeito real (regra §5.1), tratados como
sub-passo de qualquer task, não como task própria à parte. §4 (templates) → cada task de Onda 2 mapeia
seus arquivos ao template certo (Lista/Detalhe/Dashboard/Matriz/Público/Pilar); os dois templates
ainda não exercitados nos pilotos (Dashboard §4.4, Público §4.6, Pilar §4.1) entram nas Tasks 1–21 pela
primeira vez em produção — atenção redobrada nelas para achados de componente. §5 (estratégia) →
Ondas 2–3 são este plano inteiro; §5.2 coexistência aceita durante as 21 tasks; §5.3 worktrees ativas
é constraint global. §6 (verificação) → passo padrão 3 de toda task de Onda 2 + Task 22. §7
(documentação) → Task 25. Critérios de pronto (todos os 6) → Task 26.

**Placeholders:** nenhum "TBD/TODO". O Guia de conversão usa código real extraído do repositório
(`PgrsTab.tsx`, `InventarioTab.tsx`) como exemplo canônico em vez de pseudocódigo. As 21 tasks de Onda
2 não repetem esse código por arquivo — referenciam as 8 conversões do Guia por número, porque a
transformação é **idêntica e mecânica** por padrão encontrado (essa é, aliás, a premissa da própria
Onda 2 na spec: "repetitivo por design — é o que torna seguro"); repetir o mesmo diff 121 vezes não
adicionaria informação, só volume. Isso é diferente do antipadrão "similar à Task N" que a skill
proíbe — lá a preocupação é lógica nova e não descrita; aqui a lógica é a mesma lógica, já descrita uma
vez por completo.

**Consistência de tipos:** `Coluna<T>`, `EstadoVazioProps`, `AcaoWorkflow` (usado nas tasks que apontam
`DetailPageLayout`/`WorkflowActions`) e a assinatura de `useConfirmar()`/`StatusChip`/`FeedbackInline`
vêm de `src/ui/index.ts`, já estáveis desde a Onda 0 e exercitados sem mudança de assinatura nos 3
pilotos — nenhuma task deste plano os altera; se uma task achar que precisa, isso é por definição um
achado de componente (regra §5.1) e vira commit de fix antes do commit de página, não uma mudança de
assinatura silenciosa.

**Correção de escopo registrada:** o levantamento original da spec/plano anterior (~60 arquivos) só
contava `<Table>` cru; o levantamento refeito no início desta frente (união de 5 padrões) achou 121
arquivos. Ver seção "Correção ao levantamento da spec" acima — a estratégia de ondas não muda, só o
número real de tasks (21 em vez de ~8 lotes de ~8 arquivos que uma leitura só de "~60 arquivos"
sugeriria).

**Itens da Onda 1 carregados para cá:** os 3 itens deferidos e registrados na spec (seção
"Aprendizados dos pilotos") — escape hatches sem confirmação, `[buscar]`/contagem do template Matriz,
erro+vazio aparecendo juntos — não viraram task própria porque nenhum bloqueia a varredura mecânica;
ficam como decisão a tomar **durante** as Tasks 1, 4, 13 (que tocam páginas de Matriz ou repetem o
padrão erro+vazio) ou, se o volume de ocorrências for alto, como ruling explícito registrado no
relatório de fechamento (Task 26) para uma frente futura de refinamento de templates.

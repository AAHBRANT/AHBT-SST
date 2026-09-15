# Padrão de formulário inline — Onda A (piloto) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Construir o composto `PainelCriacaoInline` e o helper `valoresPadrao`, e migrar `AtividadesTab.tsx` do `PainelLateral` (drawer) para o padrão inline — piloto da eliminação do painel lateral em todo o sistema.

**Architecture:** Um composto novo em `src/ui/compostos/PainelCriacaoInline/` reaproveita `Card` + `AnimatePresence`/`framer-motion` para abrir/fechar um formulário acima da lista, em vez de um `OverlayDrawer` cobrindo a tela. `AtividadesTab.tsx` troca `PainelLateral` por esse composto, mantendo `FormGrid`/`Campo`/`Field` como estão e adicionando `FormSection`/`FormRodape` (já usados em outras ~40 telas). Um helper novo, `src/lib/valoresPadrao.ts`, centraliza a regra "Select com uma única opção possível no contexto atual vem pré-selecionado" usada nesta migração.

**Tech Stack:** React 19 + TypeScript + Fluent UI v9 + framer-motion (já em uso), Vite. Sem test runner de unidade neste projeto (confirmado: só `tsc -b`, `oxlint` e snapshots visuais via Playwright em `tests-ui/`) — a verificação de comportamento é checagem de tipo + lint + snapshot visual da galeria + checagem manual no navegador (preview do app).

**Spec:** `docs/superpowers/specs/2026-09-11-padrao-formulario-inline-design.md` (Onda A da seção 3). As ondas B–F (26 telas restantes, auditoria de pré-preenchimento, docs) ficam para planos separados depois que esta for validada com o usuário.

**Diretório-base:** todos os caminhos de arquivo abaixo são relativos a `src/AAHBRANT.SST.TeamsApp/` na raiz do repositório (`C:\Projetos\SST-APP`), salvo indicação contrária.

## Global Constraints

- Páginas (`src/pages/**`) importam só de `@ui`, nunca `@fluentui/react-components` direto (spec 07/09 §4.2; lint `no-restricted-imports` está em `warn`, não `error`, mas o padrão vale mesmo assim).
- `src/ui/**` nunca importa `lib/api` nem `pages/**` — componentes recebem dado pronto por props (spec 07/09 §2.2).
- Nomenclatura em português nos componentes novos; props em português (`aberto`, `titulo`) — convenção já usada em `PainelLateral`/`FormSection` (spec 07/09 §4.5).
- Um componente por arquivo; arquivo com o nome do componente.
- Movimento: usar os presets de `ui/tokens/movimento.ts` (`transicaoNormal`), nunca `transition={{ duration }}` escrito à mão na página/componente (spec 07/09 §1.5).
- Nenhum `az acr build`/`containerapp update` nesta onda — a entrega desta plano é local, verificada no navegador e aprovada pelo usuário antes de qualquer deploy.
- Não remover `src/ui/compostos/PainelLateral/PainelLateral.tsx` nem seu uso nas outras 25 telas — esta onda só cria o padrão novo e migra 1 tela piloto.

---

## Task 1: Helper de valores padrão (`lib/valoresPadrao.ts`)

**Files:**
- Create: `src/lib/valoresPadrao.ts`

**Interfaces:**
- Consumes: nada (função pura, sem dependência de projeto).
- Produces: `hoje(): string` (data de hoje em `yyyy-MM-dd`), `valorUnico<T>(itens: T[], id: (item: T) => string): string` (id do único item da lista, ou `''` se a lista não tiver exatamente 1 item) — a Task 5 usa os dois.

- [ ] **Step 1: Criar o arquivo com as duas funções**

```ts
// Valores padrão de formulário (spec 2026-09-11): centraliza regras que antes eram duplicadas por
// arquivo — ex.: `const hoje = () => new Date().toISOString().slice(0, 10)` em InstalacoesTab.tsx.
// A Onda F do spec audita e migra os usos duplicados existentes para cá.

/** Data de hoje em yyyy-MM-dd, o formato que a API e o CampoData esperam. */
export function hoje(): string {
  return new Date().toISOString().slice(0, 10);
}

/**
 * Id do único item de `itens`, ou '' se a lista estiver vazia ou tiver mais de um item.
 * Regra "Select com uma única opção possível no contexto atual vem pré-selecionado" (spec §2).
 */
export function valorUnico<T>(itens: T[], id: (item: T) => string): string {
  return itens.length === 1 ? id(itens[0]) : '';
}
```

- [ ] **Step 2: Checar tipo**

Run: `npx tsc -b --noEmit` (a partir de `src/AAHBRANT.SST.TeamsApp/`)
Expected: sem erros novos relacionados a `valoresPadrao.ts`.

- [ ] **Step 3: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/lib/valoresPadrao.ts
git commit -m "feat(ui): helper de valores padrao de formulario (hoje, valorUnico)"
```

---

## Task 2: Componente `PainelCriacaoInline`

**Files:**
- Create: `src/ui/compostos/PainelCriacaoInline/PainelCriacaoInline.tsx`
- Modify: `src/ui/index.ts:56` (junto da linha do `PainelLateral`)

**Interfaces:**
- Consumes: `Card` (`../Card/Card`), `transicaoNormal` (`../../tokens/movimento`), `motion`/`AnimatePresence` de `framer-motion` (já é dependência do projeto — ver `WorkflowActions.tsx`).
- Produces: `PainelCriacaoInline({ aberto, titulo, children }: PainelCriacaoInlineProps)` e `export interface PainelCriacaoInlineProps { aberto: boolean; titulo: ReactNode; children: ReactNode }` — a Task 5 consome os dois.

- [ ] **Step 1: Criar o componente**

```tsx
import type { ReactNode } from 'react';
import { AnimatePresence, motion } from 'framer-motion';
import { Card } from '../Card/Card';
import { transicaoNormal } from '../../tokens/movimento';

export interface PainelCriacaoInlineProps {
  aberto: boolean;
  titulo: ReactNode;
  children: ReactNode;
}

// Formulário de criação embutido na página (spec 2026-09-11, Onda A): substitui o PainelLateral
// para "Novo X" — cresce acima da lista/tabela em vez de cobrir a tela com um drawer, sem o vão
// vazio que um drawer de altura total cria quando o formulário é curto (achado em tablet,
// AtividadesTab). Mesmo mecanismo de "formulário cresce de onde foi acionado" que
// ui/layout/WorkflowActions/WorkflowActions.tsx já usa (altura + opacidade, transicaoNormal).
export function PainelCriacaoInline({ aberto, titulo, children }: PainelCriacaoInlineProps) {
  return (
    <AnimatePresence initial={false}>
      {aberto && (
        <motion.div
          key="painel-criacao-inline"
          initial={{ height: 0, opacity: 0 }}
          animate={{ height: 'auto', opacity: 1 }}
          exit={{ height: 0, opacity: 0 }}
          transition={transicaoNormal}
          style={{ overflow: 'hidden' }}
        >
          <Card titulo={titulo}>{children}</Card>
        </motion.div>
      )}
    </AnimatePresence>
  );
}
```

- [ ] **Step 2: Exportar em `ui/index.ts`**

Em `src/ui/index.ts:56`, logo abaixo da linha do `PainelLateral`:

```ts
export { PainelLateral, type PainelLateralProps } from './compostos/PainelLateral/PainelLateral';
export { PainelCriacaoInline, type PainelCriacaoInlineProps } from './compostos/PainelCriacaoInline/PainelCriacaoInline';
```

- [ ] **Step 3: Checar tipo e lint**

Run (a partir de `src/AAHBRANT.SST.TeamsApp/`): `npx tsc -b --noEmit && npx oxlint`
Expected: sem erros novos.

- [ ] **Step 4: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/ui/compostos/PainelCriacaoInline/PainelCriacaoInline.tsx src/AAHBRANT.SST.TeamsApp/src/ui/index.ts
git commit -m "feat(ui): componente PainelCriacaoInline (formulario de criacao inline com toggle)"
```

---

## Task 3: Seção na galeria + snapshot visual

**Files:**
- Modify: `src/ui/galeria/GaleriaPage.tsx`
- Modify: `tests-ui/galeria.spec.ts`

**Interfaces:**
- Consumes: `PainelCriacaoInline` (Task 2), padrão de `?abrir=` já existente em `GaleriaPage.tsx:54-59` (ex.: `abrir === 'painel'` para `painelAberto`).
- Produces: nova seção `data-secao="painel-criacao-inline"` na galeria; snapshot de referência commitado.

- [ ] **Step 1: Adicionar estado de abertura ligado a `?abrir=`**

Em `src/ui/galeria/GaleriaPage.tsx`, junto da linha 56 (`const [painelAberto, setPainelAberto] = useState(abrir === 'painel');`):

```tsx
const [painelAberto, setPainelAberto] = useState(abrir === 'painel');
const [painelCriacaoAberto, setPainelCriacaoAberto] = useState(abrir === 'painel-criacao-inline');
```

- [ ] **Step 2: Importar o componente**

Em `src/ui/galeria/GaleriaPage.tsx:6`, adicionar `PainelCriacaoInline` à lista de imports de `../index` (mesma linha longa dos outros imports).

- [ ] **Step 3: Adicionar a seção**

Logo após a seção `painel-lateral` (fecha em `GaleriaPage.tsx:254`, antes de `<Secao id="kpi-card"`):

```tsx
<Secao id="painel-criacao-inline" titulo="PainelCriacaoInline">
  <div className={g.larguraTotal}>
    <Button appearance="primary" onClick={() => setPainelCriacaoAberto((a) => !a)}>
      {painelCriacaoAberto ? 'Fechar formulário' : '+ Novo item'}
    </Button>
    <div className={g.margemTopo}>
      <PainelCriacaoInline aberto={painelCriacaoAberto} titulo="Novo item">
        <FormSection titulo="Dados" numero={1} primeira>
          <FormGrid>
            <Campo span={6}><Field label="Nome"><Input /></Field></Campo>
            <Campo span={6}><Field label="Quantidade"><Input type="number" defaultValue="1" /></Field></Campo>
          </FormGrid>
          <FormRodape>
            <Button onClick={() => setPainelCriacaoAberto(false)}>Cancelar</Button>
            <Button appearance="primary" onClick={() => setPainelCriacaoAberto(false)}>Adicionar</Button>
          </FormRodape>
        </FormSection>
      </PainelCriacaoInline>
    </div>
  </div>
</Secao>
```

- [ ] **Step 4: Atualizar a contagem de seções no snapshot spec**

Em `tests-ui/galeria.spec.ts:14`, trocar:

```ts
expect(total).toBe(21);
```

por:

```ts
expect(total).toBe(22);
```

- [ ] **Step 5: Adicionar teste de snapshot da variante aberta**

Em `tests-ui/galeria.spec.ts`, logo após o teste `painel lateral aberto em tema ${tema}` (linhas 25-32), adicionar:

```ts
  test(`painel de criação inline aberto em tema ${tema}`, async ({ page }) => {
    await page.addInitScript((t) => localStorage.setItem('sst.modoTema', t), tema);
    await page.goto('/#/ui-galeria?abrir=painel-criacao-inline');
    await page.evaluate(async () => { await document.fonts.ready; });
    const secao = page.locator('section[data-secao="painel-criacao-inline"]');
    await secao.scrollIntoViewIfNeeded();
    await expect(secao).toHaveScreenshot(`${tema}-painel-criacao-inline-aberto.png`);
  });
```

- [ ] **Step 6: Gerar os snapshots de referência**

Run (a partir de `src/AAHBRANT.SST.TeamsApp/`): `npx playwright test -c playwright.config.ts --update-snapshots`

Expected: passa e cria/atualiza os PNGs em `tests-ui/galeria.spec.ts-snapshots/` (2 temas × seção nova = 4 arquivos novos: a seção fechada já entra no loop principal, mais o snapshot aberto). Se o Playwright não tiver o Chromium instalado neste ambiente, rodar `npx playwright install chromium` primeiro e repetir. Se mesmo assim não for possível rodar (sandbox sem navegador), pular este passo e registrar no relatório da tarefa — a verificação manual da Task 5 (abrir a galeria pelo preview) cobre o mesmo caso visualmente.

- [ ] **Step 7: Rodar a suíte completa para conferir que nada mais quebrou**

Run: `npx playwright test -c playwright.config.ts`
Expected: todos os testes passam (inclusive os 21 já existentes, agora contra a seção 22).

- [ ] **Step 8: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/ui/galeria/GaleriaPage.tsx src/AAHBRANT.SST.TeamsApp/tests-ui/galeria.spec.ts src/AAHBRANT.SST.TeamsApp/tests-ui/galeria.spec.ts-snapshots
git commit -m "docs(ui): PainelCriacaoInline na galeria + snapshot visual"
```

---

## Task 4: Migrar `AtividadesTab.tsx`

**Files:**
- Modify: `src/pages/riscos/AtividadesTab.tsx` (arquivo inteiro tem 184 linhas — trocado quase por completo abaixo)

**Interfaces:**
- Consumes: `PainelCriacaoInline` (Task 2), `hoje`/`valorUnico` de `../../lib/valoresPadrao` (Task 1) — só `valorUnico` é usado aqui (`Atividade` não tem campo de data).
- Produces: nada consumido por outra task desta onda.

- [ ] **Step 1: Trocar os imports**

Em `src/pages/riscos/AtividadesTab.tsx:1-19`, substituir o bloco de import de `@ui` (linhas 2-16) por:

```tsx
import { useEffect, useState } from 'react';
import {
  Button,
  Card,
  Campo,
  DataTable,
  Field,
  FeedbackInline,
  FormGrid,
  FormRodape,
  FormSection,
  Input,
  PageHeader,
  PainelCriacaoInline,
  Select,
  useConfirmar,
  type Coluna,
} from '@ui';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import { api, type Atividade, type NovaAtividade, type Obra } from '../../lib/api';
import { valorUnico } from '../../lib/valoresPadrao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
```

- [ ] **Step 2: Atualizar o comentário de topo do componente e pré-selecionar Obra única**

Em `src/pages/riscos/AtividadesTab.tsx:23-25` (comentário) e dentro de `carregar()` (linhas 38-49), substituir:

```tsx
// Camada ui/ (Onda 2, Task 13): mesmo padrão de FuncoesTab.tsx (Task 1) — formulário de 3 campos que
// empurrava a lista pra baixo saiu para um PainelLateral aberto pelo "+ Adicionar atividade" do
// PageHeader. Erro do formulário fica em estado próprio, separado do erro de carga da lista.
export function AtividadesTab() {
```

por:

```tsx
// Onda A do spec de formulário inline (2026-09-11): o PainelLateral (drawer) saiu — em tablet, o
// formulário de 3 campos esticava até a altura total da tela e sobrava um vão vazio enorme antes
// dos botões. Agora é um PainelCriacaoInline, que cresce acima da lista só até a altura do próprio
// formulário. Erro do formulário fica em estado próprio, separado do erro de carga da lista.
export function AtividadesTab() {
```

e, em `carregar()`:

```tsx
  async function carregar() {
    try {
      setErro(null);
      const [ativs, obrs] = await Promise.all([api.atividades.listar(), api.obras.listar()]);
      setAtividades(ativs);
      setObras(obrs);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar atividades.');
    } finally {
      setCarregandoLista(false);
    }
  }
```

por:

```tsx
  async function carregar() {
    try {
      setErro(null);
      const [ativs, obrs] = await Promise.all([api.atividades.listar(), api.obras.listar()]);
      setAtividades(ativs);
      setObras(obrs);
      // Select com uma única opção possível no contexto atual vem pré-selecionado (spec §2). Não
      // sobrescreve se o usuário já tiver escolhido uma Obra.
      setNovaAtividade((prev) => (prev.obraId ? prev : { ...prev, obraId: valorUnico(obrs, (o) => o.id) }));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar atividades.');
    } finally {
      setCarregandoLista(false);
    }
  }
```

- [ ] **Step 3: Trocar o botão do `PageHeader` para alternar (toggle) o painel**

Em `src/pages/riscos/AtividadesTab.tsx:100-107`, trocar:

```tsx
        acoes={
          <Button appearance="primary" icon={<Add24Regular />} onClick={() => setPainelAberto(true)}>
            Adicionar atividade
          </Button>
        }
```

por:

```tsx
        acoes={
          <Button appearance="primary" icon={<Add24Regular />} onClick={() => setPainelAberto((a) => !a)}>
            {painelAberto ? 'Fechar' : 'Adicionar atividade'}
          </Button>
        }
```

- [ ] **Step 4: Trocar `PainelLateral` por `PainelCriacaoInline` com `FormSection`/`FormRodape`**

Em `src/pages/riscos/AtividadesTab.tsx:113-180`, o bloco que vai do `<Card>` da lista até o fechamento do `</PainelLateral>` passa a:

```tsx
      <PainelCriacaoInline aberto={painelAberto} titulo="Nova atividade">
        <FormSection titulo="Dados da atividade" numero={1} primeira>
          {erroPainel && (
            <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
              {erroPainel}
            </FeedbackInline>
          )}
          <FormGrid>
            <Campo span={3}>
              <Field label="Obra">
                <Select
                  value={novaAtividade.obraId}
                  onChange={(_, d) => setNovaAtividade({ ...novaAtividade, obraId: d.value })}
                >
                  <option value="">Selecione</option>
                  {obras.map((obra) => (
                    <option key={obra.id} value={obra.id}>
                      {obra.nome}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={4}>
              <Field label="Nome da atividade">
                <Input
                  value={novaAtividade.nome}
                  onChange={(_, d) => setNovaAtividade({ ...novaAtividade, nome: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={5}>
              <Field label="Descrição">
                <Input
                  value={novaAtividade.descricao ?? ''}
                  onChange={(_, d) => setNovaAtividade({ ...novaAtividade, descricao: d.value })}
                />
              </Field>
            </Campo>
          </FormGrid>
          <FormRodape>
            <Button onClick={fecharPainel}>Cancelar</Button>
            <Button appearance="primary" onClick={criar} disabled={carregando}>
              Adicionar atividade
            </Button>
          </FormRodape>
        </FormSection>
      </PainelCriacaoInline>
      <Card>
        <DataTable
          aria-label="Atividades cadastradas"
          colunas={colunas}
          linhas={atividades}
          chaveLinha={(a) => a.id}
          carregando={carregandoLista}
          vazio={{
            titulo: 'Nenhuma atividade cadastrada ainda',
            acao: { rotulo: 'Adicionar atividade', aoClicar: () => setPainelAberto(true) },
          }}
          acoesLinha={(a) => (
            <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluir(a.id)} aria-label="Excluir" />
          )}
        />
      </Card>
```

(O `PainelCriacaoInline` fica **antes** do `<Card>` da lista — o formulário de criação sai de cima da tabela, seguindo o mesmo lugar do "Nova instalação" em `InstalacoesTab.tsx`.)

- [ ] **Step 5: Envolver o `return` num container com espaçamento**

Em `src/pages/riscos/AtividadesTab.tsx:97-98`, trocar `<div>` (abertura) por:

```tsx
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
```

E o fechamento `</div>` no final do `return` continua igual.

- [ ] **Step 6: Checar tipo e lint**

Run (a partir de `src/AAHBRANT.SST.TeamsApp/`): `npx tsc -b --noEmit && npx oxlint`
Expected: sem erros. Se `oxlint` acusar import não usado, confirme que `PainelLateral` não ficou em nenhum import remanescente.

- [ ] **Step 7: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/pages/riscos/AtividadesTab.tsx
git commit -m "feat(riscos): migra Nova atividade do PainelLateral para PainelCriacaoInline"
```

---

## Task 5: Verificação visual manual (desktop + tablet)

**Files:** nenhum (só verificação — não edita código).

**Interfaces:**
- Consumes: app rodando via `npm run dev` (porta padrão do projeto), rota de Gestão de SST → PGR/GRO → Atividades.

- [ ] **Step 1: Subir o preview do app** (dev server do projeto, ver `.claude/launch.json` ou `npm run dev` em `src/AAHBRANT.SST.TeamsApp/`)

- [ ] **Step 2: Abrir a tela de Atividades** (Gestão de SST → PGR/GRO → Atividades) em viewport desktop, clicar em "Adicionar atividade" e confirmar: o formulário aparece **acima** da tabela, sem vão vazio, com a "Etapa 1 - Dados da atividade" visível; clicar de novo fecha (toggle); "Cancelar" fecha sem enviar; preencher e enviar cria a atividade e fecha o painel.

- [ ] **Step 3: Repetir em viewport tablet** (ex.: 834×1194 — iPad em retrato) e confirmar que não sobra espaço vazio entre os campos e o rodapé, e que a rolagem da página continua natural (sem drawer cobrindo a tela).

- [ ] **Step 4: Se houver só 1 Obra cadastrada nos dados de teste, confirmar que o campo Obra já vem pré-selecionado ao abrir o formulário.** Se houver mais de uma Obra (esperado nos dados mock atuais), essa regra é esperada **não** disparar aqui — a demonstração com 1 item fica na seção `painel-criacao-inline` da galeria (Task 3).

- [ ] **Step 5: Reportar ao usuário** com uma captura de tela de cada viewport antes de considerar a Onda A concluída — nenhum deploy é feito nesta tarefa.

---

## Self-Review Notes (preenchido durante a escrita do plano)

- **Cobertura do spec (seção 3, Onda A):** "Nova atividade" migrada (Task 4), `PainelCriacaoInline` construído (Task 2), verificação desktop+tablet (Task 5) — cobertos. `lib/valoresPadrao.ts` (Task 1) cobre a convenção de pré-preenchimento da seção 2 do spec, na medida do que esta tela tem (sem campo de data, só Obra única).
- **Placeholders:** nenhum "TBD"/"implementar depois" — todo passo tem código completo.
- **Consistência de tipos:** `PainelCriacaoInlineProps` definido na Task 2 é o mesmo usado nas Tasks 3 e 4; `valorUnico`/`hoje` definidos na Task 1 com as assinaturas usadas na Task 4 (só `valorUnico` é chamada; `hoje` fica pronta para a Onda F, sem uso nesta onda — mantida no helper porque a Onda F vai substituir o `hoje()` duplicado de `InstalacoesTab.tsx` por este mesmo import).
- **Fora de escopo, de propósito:** as outras 25 telas com `PainelLateral`, a auditoria de pré-preenchimento das ~40 telas inline existentes, e a atualização de `docs/design-system.md` — tudo isso é Onda B em diante, com plano próprio depois que o usuário validar esta Onda A.

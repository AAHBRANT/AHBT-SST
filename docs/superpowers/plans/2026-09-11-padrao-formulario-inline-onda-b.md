# Padrão de formulário inline — Onda B (Pessoas) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Estender `PainelCriacaoInline` com `id`/`subtitulo` e o hook `usePainelCriacaoInline` (addendum pós-Onda A), e migrar as 6 telas do módulo Pessoas (Trabalhadores, Funções, Equipes, Setores, Cursos de Treinamento, Treinamentos) do `PainelLateral` para esse padrão.

**Architecture:** Task 1 estende o composto já existente (`ui/compostos/PainelCriacaoInline/`) de forma aditiva e retrocompatível — `AtividadesTab.tsx` (Onda A) não muda. As Tasks 2-7 migram cada tela, uma por vez, seguindo o mesmo molde: `useState<boolean>` vira `usePainelCriacaoInline()`; o botão que abre/fecha o painel (no `PageHeader` ou no `acoes` de um `Card`) ganha `aria-expanded`/`aria-controls`; o `PainelLateral` vira `PainelCriacaoInline` com `FormSection`/`FormRodape` dentro, posicionado antes do `Card` da lista; campos relacionais com uma única opção possível ganham `valorUnico()`.

**Tech Stack:** React 19 + TypeScript + Fluent UI v9 + framer-motion, Vite. Sem test runner de unidade — verificação é `npx tsc -b --noEmit && npx oxlint` a partir de `src/AAHBRANT.SST.TeamsApp/`, mais checagem manual no navegador (Task 8).

**Spec:** `docs/superpowers/specs/2026-09-11-padrao-formulario-inline-design.md` (seção 5, addendum pós-Onda A, para a Task 1; seção 3, Onda B, para as telas).

**Diretório-base:** todos os caminhos de arquivo abaixo são relativos a `src/AAHBRANT.SST.TeamsApp/` na raiz do repositório (`C:\Projetos\SST-APP`), salvo indicação contrária.

## Global Constraints

- Páginas (`src/pages/**`) importam só de `@ui`, nunca `@fluentui/react-components` direto.
- `src/ui/**` nunca importa `lib/api` nem `pages/**`.
- Nomenclatura em português; props em português (`aberto`, `titulo`, `subtitulo`).
- Movimento: só os presets de `ui/tokens/movimento.ts` (`transicaoNormal`), nunca `transition={{ duration }}` na mão.
- `PainelCriacaoInlineProps` existente (`aberto`, `titulo`, `children`) não perde compatibilidade — `id`/`subtitulo` são opcionais e aditivos. `AtividadesTab.tsx` (Onda A) **não é tocado** nesta onda.
- `TrabalhadoresGaveta.tsx` **fica fora do escopo** — é um drawer de busca/navegação global (aberto pela topbar do `AppShell`), não um formulário de criação; não migra para `PainelCriacaoInline`.
- `valorUnico()` só em campos relacionais **obrigatórios**; nunca em campos opcionais com "sem seleção" como estado válido (ex.: Encarregado em `EquipesTab.tsx`).
- Nenhum `az acr build`/`containerapp update` nesta onda sem o usuário ver rodando localmente e confirmar.
- Não remover `src/ui/compostos/PainelLateral/PainelLateral.tsx` — outras ~19 telas (26 originais − 6 desta onda − a da Onda A) continuam usando.

---

## Task 1: Estender `PainelCriacaoInline` + hook `usePainelCriacaoInline`

**Files:**
- Modify: `src/ui/compostos/PainelCriacaoInline/PainelCriacaoInline.tsx`
- Create: `src/ui/compostos/PainelCriacaoInline/usePainelCriacaoInline.ts`
- Modify: `src/ui/index.ts`

**Interfaces:**
- Consumes: `Card` (`../Card/Card`, já aceita `subtitulo?: ReactNode`), `useId`/`useState` de `react`.
- Produces: `PainelCriacaoInlineProps` com `id?: string` e `subtitulo?: ReactNode` novos (aditivos); `usePainelCriacaoInline(): { aberto: boolean; id: string; abrir: () => void; fechar: () => void; alternar: () => void }` — as Tasks 2-7 consomem os dois.

- [ ] **Step 1: Adicionar `id`/`subtitulo` ao componente, com wrapper sempre presente no DOM**

Substituir todo o conteúdo de `src/ui/compostos/PainelCriacaoInline/PainelCriacaoInline.tsx` por:

```tsx
import type { ReactNode } from 'react';
import { AnimatePresence, motion } from 'framer-motion';
import { Card } from '../Card/Card';
import { transicaoNormal } from '../../tokens/movimento';

export interface PainelCriacaoInlineProps {
  aberto: boolean;
  titulo: ReactNode;
  subtitulo?: ReactNode;
  /** Alvo de aria-controls do botão que abre/fecha o painel (ver usePainelCriacaoInline). Fica no
   *  wrapper externo, sempre presente no DOM — mesmo com aberto=false — para o aria-controls do
   *  botão sempre apontar para um elemento que existe. */
  id?: string;
  children: ReactNode;
}

// Formulário de criação embutido na página (spec 2026-09-11, Onda A): substitui o PainelLateral
// para "Novo X" — cresce acima da lista/tabela em vez de cobrir a tela com um drawer, sem o vão
// vazio que um drawer de altura total cria quando o formulário é curto (achado em tablet,
// AtividadesTab). Mesmo mecanismo de "formulário cresce de onde foi acionado" que
// ui/layout/WorkflowActions/WorkflowActions.tsx já usa (altura + opacidade, transicaoNormal).
// id/subtitulo (addendum pós-Onda A, spec §5): aditivos, para o hook usePainelCriacaoInline e para
// o caso de FuncoesTab.tsx, que tinha um subtitulo no PainelLateral antigo.
export function PainelCriacaoInline({ aberto, titulo, subtitulo, id, children }: PainelCriacaoInlineProps) {
  return (
    <div id={id}>
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
            <Card titulo={titulo} subtitulo={subtitulo}>
              {children}
            </Card>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
```

- [ ] **Step 2: Criar o hook**

```ts
import { useId, useState } from 'react';

export interface PainelCriacaoInlineControle {
  aberto: boolean;
  id: string;
  abrir: () => void;
  fechar: () => void;
  alternar: () => void;
}

// Estado + id estável de um PainelCriacaoInline (spec 2026-09-11, addendum pós-Onda A): centraliza
// o useState<boolean> e o id de aria-controls que cada tela repetia à mão. Não sabe de erroPainel
// (estado específico de cada página) — a própria tela chama fechar() dentro do seu fecharPainel()
// local, que também limpa o erro.
export function usePainelCriacaoInline(): PainelCriacaoInlineControle {
  const [aberto, setAberto] = useState(false);
  const id = useId();
  return {
    aberto,
    id,
    abrir: () => setAberto(true),
    fechar: () => setAberto(false),
    alternar: () => setAberto((a) => !a),
  };
}
```

- [ ] **Step 3: Exportar em `ui/index.ts`**

Em `src/ui/index.ts`, na linha do `PainelCriacaoInline` (adicionada na Onda A), trocar:

```ts
export { PainelCriacaoInline, type PainelCriacaoInlineProps } from './compostos/PainelCriacaoInline/PainelCriacaoInline';
```

por:

```ts
export { PainelCriacaoInline, type PainelCriacaoInlineProps } from './compostos/PainelCriacaoInline/PainelCriacaoInline';
export { usePainelCriacaoInline, type PainelCriacaoInlineControle } from './compostos/PainelCriacaoInline/usePainelCriacaoInline';
```

- [ ] **Step 4: Checar tipo e lint**

Run (a partir de `src/AAHBRANT.SST.TeamsApp/`): `npx tsc -b --noEmit && npx oxlint`
Expected: sem erros novos. `AtividadesTab.tsx` continua compilando sem alteração (props novas são opcionais).

- [ ] **Step 5: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/ui/compostos/PainelCriacaoInline/PainelCriacaoInline.tsx src/AAHBRANT.SST.TeamsApp/src/ui/compostos/PainelCriacaoInline/usePainelCriacaoInline.ts src/AAHBRANT.SST.TeamsApp/src/ui/index.ts
git commit -m "feat(ui): id/subtitulo em PainelCriacaoInline + hook usePainelCriacaoInline"
```

**Fora do escopo desta task:** a galeria (`ui-galeria`) não é atualizada — a seção já existente demonstra o comportamento base; as 6 telas da Onda B são o exercício real da API nova, e mexer na galeria geraria churn de snapshot sem necessidade.

---

## Task 2: Migrar `FuncoesTab.tsx`

**Files:**
- Modify: `src/pages/pessoas/FuncoesTab.tsx`

**Interfaces:**
- Consumes: `PainelCriacaoInline`, `usePainelCriacaoInline` (Task 1).
- Produces: nada consumido por outra task.

- [ ] **Step 1: Trocar os imports**

Substituir as linhas 1-18 de `src/pages/pessoas/FuncoesTab.tsx` por:

```tsx
import { useEffect, useState } from 'react';
import {
  Button,
  Field,
  Input,
  Card,
  PageHeader,
  DataTable,
  PainelCriacaoInline,
  usePainelCriacaoInline,
  FormSection,
  FormGrid,
  FormRodape,
  Campo,
  FeedbackInline,
  useConfirmar,
  type Coluna,
} from '@ui';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import { api, type Funcao, type NovaFuncao } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';
```

- [ ] **Step 2: Trocar o `useState`/comentário por `usePainelCriacaoInline`**

Substituir:

```tsx
// A matriz de EPI por função fica no módulo EPI (ver MatrizEpiTab.tsx em pages/epi) — aqui é só o
// cadastro (CRUD) da função em si, usado também por Trabalhadores/Equipes. Camada ui/ (Onda 2,
// Task 1): formulário de criação saiu de cima da tabela (empurrava a lista pra baixo) e foi para um
// PainelLateral, aberto pelo "+ Adicionar função" do PageHeader — mesmo padrão do piloto 1
// (EntregasTab). Erro do formulário fica em estado próprio, separado do erro de carga da lista.
export function FuncoesTab() {
  const [funcoes, setFuncoes] = useState<Funcao[]>([]);
  const [novaFuncao, setNovaFuncao] = useState<NovaFuncao>(funcaoVazia);
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [painelAberto, setPainelAberto] = useState(false);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();
```

por:

```tsx
// A matriz de EPI por função fica no módulo EPI (ver MatrizEpiTab.tsx em pages/epi) — aqui é só o
// cadastro (CRUD) da função em si, usado também por Trabalhadores/Equipes. Onda B do spec de
// formulário inline (2026-09-11): PainelLateral -> PainelCriacaoInline. Erro do formulário fica em
// estado próprio, separado do erro de carga da lista.
export function FuncoesTab() {
  const [funcoes, setFuncoes] = useState<Funcao[]>([]);
  const [novaFuncao, setNovaFuncao] = useState<NovaFuncao>(funcaoVazia);
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const painel = usePainelCriacaoInline();
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();
```

- [ ] **Step 3: Atualizar `fecharPainel`**

Substituir:

```tsx
  function fecharPainel() {
    setPainelAberto(false);
    setErroPainel(null);
  }
```

por:

```tsx
  function fecharPainel() {
    painel.fechar();
    setErroPainel(null);
  }
```

- [ ] **Step 4: Atualizar o `PageHeader`, o estado vazio e trocar `PainelLateral` por `PainelCriacaoInline`**

Substituir todo o bloco do `return` (linhas 92-168 do arquivo original) por:

```tsx
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {dialogElement}
      <PageHeader
        titulo="Funções cadastradas"
        acoes={
          <Button
            appearance="primary"
            icon={<Add24Regular />}
            onClick={() => (painel.aberto ? fecharPainel() : painel.abrir())}
            aria-expanded={painel.aberto}
            aria-controls={painel.id}
          >
            {painel.aberto ? 'Fechar' : 'Adicionar função'}
          </Button>
        }
      />
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}
      <PainelCriacaoInline
        aberto={painel.aberto}
        id={painel.id}
        titulo="Nova função"
        subtitulo="A matriz de EPI de cada função é definida em EPI → Matriz de EPI por Função."
      >
        <FormSection titulo="Dados da função" numero={1} primeira>
          {erroPainel && (
            <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
              {erroPainel}
            </FeedbackInline>
          )}
          <FormGrid>
            <Campo span={6}>
              <Field label="Nome">
                <Input value={novaFuncao.nome} onChange={(_, d) => setNovaFuncao({ ...novaFuncao, nome: d.value })} />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Código CBO">
                <Input
                  value={novaFuncao.cboCodigo ?? ''}
                  onChange={(_, d) => setNovaFuncao({ ...novaFuncao, cboCodigo: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Descrição">
                <Input
                  value={novaFuncao.descricao ?? ''}
                  onChange={(_, d) => setNovaFuncao({ ...novaFuncao, descricao: d.value })}
                />
              </Field>
            </Campo>
          </FormGrid>
          <FormRodape>
            <Button onClick={fecharPainel}>Cancelar</Button>
            <Button appearance="primary" onClick={criar} disabled={carregando}>
              Adicionar função
            </Button>
          </FormRodape>
        </FormSection>
      </PainelCriacaoInline>
      <Card>
        <DataTable
          aria-label="Funções cadastradas"
          colunas={colunas}
          linhas={funcoes}
          chaveLinha={(f) => f.id}
          carregando={carregandoLista}
          vazio={{
            titulo: 'Nenhuma função cadastrada ainda',
            acao: { rotulo: 'Adicionar função', aoClicar: () => painel.abrir() },
          }}
          acoesLinha={(f) => (
            <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluir(f.id)} aria-label="Excluir" />
          )}
        />
      </Card>
    </div>
  );
}
```

(O `criar()` já chama `fecharPainel()` no final — nenhuma mudança necessária nele.)

- [ ] **Step 5: Checar tipo e lint**

Run: `npx tsc -b --noEmit && npx oxlint`
Expected: sem erros.

- [ ] **Step 6: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/pages/pessoas/FuncoesTab.tsx
git commit -m "feat(pessoas): migra Nova funcao do PainelLateral para PainelCriacaoInline"
```

---

## Task 3: Migrar `EquipesTab.tsx`

**Files:**
- Modify: `src/pages/pessoas/EquipesTab.tsx`

**Interfaces:**
- Consumes: `PainelCriacaoInline`, `usePainelCriacaoInline` (Task 1); `valorUnico` de `../../lib/valoresPadrao` (Onda A, Task 1).
- Produces: nada consumido por outra task.

- [ ] **Step 1: Trocar os imports**

Substituir as linhas 1-19 por:

```tsx
import { useEffect, useState } from 'react';
import {
  Button,
  Field,
  Input,
  Card,
  PageHeader,
  DataTable,
  PainelCriacaoInline,
  usePainelCriacaoInline,
  FormSection,
  FormGrid,
  FormRodape,
  Campo,
  FeedbackInline,
  SeletorPesquisavel,
  useConfirmar,
  type Coluna,
} from '@ui';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import { api, type Equipe, type NovaEquipe, type Setor, type Trabalhador } from '../../lib/api';
import { valorUnico } from '../../lib/valoresPadrao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
```

- [ ] **Step 2: Trocar `useState`/comentário e pré-selecionar Setor único em `carregar()`**

Substituir:

```tsx
// Camada ui/ (Onda 2, Task 1): formulário de criação foi para um PainelLateral. Setor e Encarregado
// (lista de trabalhadores, potencialmente grande) viram SeletorPesquisavel (spec §3); Encarregado é
// opcional, então o `opcaoVazia` "Sem encarregado definido" preserva o caminho de volta ao vazio que
// o <select> original tinha (aprendizado do piloto 2).
export function EquipesTab() {
  const [setores, setSetores] = useState<Setor[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [equipes, setEquipes] = useState<Equipe[]>([]);
  const [novaEquipe, setNovaEquipe] = useState<NovaEquipe>(equipeVazia);
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [painelAberto, setPainelAberto] = useState(false);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [setoresResp, trabalhadoresResp, equipesResp] = await Promise.all([
        api.setores.listar(),
        api.trabalhadores.listar(),
        api.equipes.listar(),
      ]);
      setSetores(setoresResp);
      setTrabalhadores(trabalhadoresResp);
      setEquipes(equipesResp);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar equipes.');
    } finally {
      setCarregandoLista(false);
    }
  }
```

por:

```tsx
// Onda B do spec de formulário inline (2026-09-11): PainelLateral -> PainelCriacaoInline. Setor e
// Encarregado (lista de trabalhadores, potencialmente grande) usam SeletorPesquisavel (spec §3);
// Encarregado é opcional, então o `opcaoVazia` "Sem encarregado definido" preserva o caminho de
// volta ao vazio.
export function EquipesTab() {
  const [setores, setSetores] = useState<Setor[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [equipes, setEquipes] = useState<Equipe[]>([]);
  const [novaEquipe, setNovaEquipe] = useState<NovaEquipe>(equipeVazia);
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const painel = usePainelCriacaoInline();
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [setoresResp, trabalhadoresResp, equipesResp] = await Promise.all([
        api.setores.listar(),
        api.trabalhadores.listar(),
        api.equipes.listar(),
      ]);
      setSetores(setoresResp);
      setTrabalhadores(trabalhadoresResp);
      setEquipes(equipesResp);
      // Select com uma única opção possível no contexto atual vem pré-selecionado (spec §2). Não
      // sobrescreve se o usuário já tiver escolhido um Setor.
      setNovaEquipe((prev) => (prev.setorId ? prev : { ...prev, setorId: valorUnico(setoresResp, (s) => s.id) }));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar equipes.');
    } finally {
      setCarregandoLista(false);
    }
  }
```

- [ ] **Step 3: Atualizar `fecharPainel`**

Substituir:

```tsx
  function fecharPainel() {
    setPainelAberto(false);
    setErroPainel(null);
  }
```

por:

```tsx
  function fecharPainel() {
    painel.fechar();
    setErroPainel(null);
  }
```

- [ ] **Step 4: Atualizar o `PageHeader`, o estado vazio e trocar `PainelLateral` por `PainelCriacaoInline`**

Substituir todo o bloco do `return` por:

```tsx
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {dialogElement}
      <PageHeader
        titulo="Equipes cadastradas"
        acoes={
          <Button
            appearance="primary"
            icon={<Add24Regular />}
            onClick={() => (painel.aberto ? fecharPainel() : painel.abrir())}
            aria-expanded={painel.aberto}
            aria-controls={painel.id}
          >
            {painel.aberto ? 'Fechar' : 'Adicionar equipe'}
          </Button>
        }
      />
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}
      <PainelCriacaoInline aberto={painel.aberto} id={painel.id} titulo="Nova equipe">
        <FormSection titulo="Dados da equipe" numero={1} primeira>
          {erroPainel && (
            <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
              {erroPainel}
            </FeedbackInline>
          )}
          <FormGrid>
            <Campo span={12}>
              <Field label="Setor" required>
                <SeletorPesquisavel
                  placeholder="Selecione o setor"
                  opcaoVazia="Selecione o setor"
                  opcoes={opcoesSetores}
                  valor={novaEquipe.setorId}
                  aoMudar={(id) => setNovaEquipe({ ...novaEquipe, setorId: id })}
                />
              </Field>
            </Campo>
            <Campo span={6}>
              <Field label="Nome da equipe">
                <Input value={novaEquipe.nome} onChange={(_, d) => setNovaEquipe({ ...novaEquipe, nome: d.value })} />
              </Field>
            </Campo>
            <Campo span={6}>
              <Field label="Encarregado (opcional)">
                <SeletorPesquisavel
                  placeholder="Sem encarregado definido"
                  opcaoVazia="Sem encarregado definido"
                  opcoes={opcoesTrabalhadores}
                  valor={novaEquipe.encarregadoId ?? ''}
                  aoMudar={(id) => setNovaEquipe({ ...novaEquipe, encarregadoId: id || null })}
                />
              </Field>
            </Campo>
          </FormGrid>
          <FormRodape>
            <Button onClick={fecharPainel}>Cancelar</Button>
            <Button appearance="primary" onClick={criar} disabled={carregando}>
              Adicionar equipe
            </Button>
          </FormRodape>
        </FormSection>
      </PainelCriacaoInline>
      <Card>
        <DataTable
          aria-label="Equipes cadastradas"
          colunas={colunas}
          linhas={equipes}
          chaveLinha={(e) => e.id}
          carregando={carregandoLista}
          vazio={{
            titulo: 'Nenhuma equipe cadastrada ainda',
            acao: { rotulo: 'Adicionar equipe', aoClicar: () => painel.abrir() },
          }}
          acoesLinha={(e) => (
            <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluir(e.id)} aria-label="Excluir" />
          )}
        />
      </Card>
    </div>
  );
}
```

(`criar()` já valida `!novaEquipe.setorId` e já chama `fecharPainel()` no final — nenhuma mudança necessária nele. A validação de "Setor obrigatório" continua funcionando mesmo com a pré-seleção: se `valorUnico()` não encontrar exatamente 1 setor, `setorId` continua vazio e a validação barra a criação, igual antes.)

- [ ] **Step 5: Checar tipo e lint**

Run: `npx tsc -b --noEmit && npx oxlint`
Expected: sem erros.

- [ ] **Step 6: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/pages/pessoas/EquipesTab.tsx
git commit -m "feat(pessoas): migra Nova equipe do PainelLateral para PainelCriacaoInline"
```

---

## Task 4: Migrar `SetoresTab.tsx`

**Files:**
- Modify: `src/pages/pessoas/SetoresTab.tsx`

**Interfaces:**
- Consumes: `PainelCriacaoInline`, `usePainelCriacaoInline` (Task 1); `valorUnico` (Onda A).
- Produces: nada consumido por outra task.

- [ ] **Step 1: Trocar os imports**

Substituir as linhas 1-19 por:

```tsx
import { useEffect, useState } from 'react';
import {
  Button,
  Field,
  Input,
  Card,
  PageHeader,
  DataTable,
  PainelCriacaoInline,
  usePainelCriacaoInline,
  FormSection,
  FormGrid,
  FormRodape,
  Campo,
  FeedbackInline,
  SeletorPesquisavel,
  useConfirmar,
  type Coluna,
} from '@ui';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import { api, type NovoSetor, type Obra, type Setor } from '../../lib/api';
import { valorUnico } from '../../lib/valoresPadrao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
```

- [ ] **Step 2: Trocar `useState`/comentário e pré-selecionar Obra única em `carregar()`**

Substituir:

```tsx
// Camada ui/ (Onda 2, Task 1): formulário de criação foi para um PainelLateral (mesmo padrão do
// piloto 1); Obra vira SeletorPesquisavel (spec §3: lista de obras é candidata a busca em vez de
// <select>), com `opcaoVazia` preservando o prompt "Selecione a obra" que o <select> tinha.
export function SetoresTab() {
  const [obras, setObras] = useState<Obra[]>([]);
  const [setores, setSetores] = useState<Setor[]>([]);
  const [novoSetor, setNovoSetor] = useState<NovoSetor>({ obraId: '', nome: '' });
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [painelAberto, setPainelAberto] = useState(false);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [obrasResp, setoresResp] = await Promise.all([api.obras.listar(), api.setores.listar()]);
      setObras(obrasResp);
      setSetores(setoresResp);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar setores.');
    } finally {
      setCarregandoLista(false);
    }
  }
```

por:

```tsx
// Onda B do spec de formulário inline (2026-09-11): PainelLateral -> PainelCriacaoInline. Obra usa
// SeletorPesquisavel (spec §3: lista de obras é candidata a busca em vez de <select>).
export function SetoresTab() {
  const [obras, setObras] = useState<Obra[]>([]);
  const [setores, setSetores] = useState<Setor[]>([]);
  const [novoSetor, setNovoSetor] = useState<NovoSetor>({ obraId: '', nome: '' });
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const painel = usePainelCriacaoInline();
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [obrasResp, setoresResp] = await Promise.all([api.obras.listar(), api.setores.listar()]);
      setObras(obrasResp);
      setSetores(setoresResp);
      // Select com uma única opção possível no contexto atual vem pré-selecionado (spec §2). Não
      // sobrescreve se o usuário já tiver escolhido uma Obra.
      setNovoSetor((prev) => (prev.obraId ? prev : { ...prev, obraId: valorUnico(obrasResp, (o) => o.id) }));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar setores.');
    } finally {
      setCarregandoLista(false);
    }
  }
```

- [ ] **Step 3: Atualizar `fecharPainel`**

Substituir:

```tsx
  function fecharPainel() {
    setPainelAberto(false);
    setErroPainel(null);
  }
```

por:

```tsx
  function fecharPainel() {
    painel.fechar();
    setErroPainel(null);
  }
```

- [ ] **Step 4: Atualizar o `PageHeader`, o estado vazio e trocar `PainelLateral` por `PainelCriacaoInline`**

Substituir todo o bloco do `return` por:

```tsx
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {dialogElement}
      <PageHeader
        titulo="Setores cadastrados"
        acoes={
          <Button
            appearance="primary"
            icon={<Add24Regular />}
            onClick={() => (painel.aberto ? fecharPainel() : painel.abrir())}
            aria-expanded={painel.aberto}
            aria-controls={painel.id}
          >
            {painel.aberto ? 'Fechar' : 'Adicionar setor'}
          </Button>
        }
      />
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}
      <PainelCriacaoInline aberto={painel.aberto} id={painel.id} titulo="Novo setor">
        <FormSection titulo="Dados do setor" numero={1} primeira>
          {erroPainel && (
            <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
              {erroPainel}
            </FeedbackInline>
          )}
          <FormGrid>
            <Campo span={6}>
              <Field label="Obra" required>
                <SeletorPesquisavel
                  placeholder="Selecione a obra"
                  opcaoVazia="Selecione a obra"
                  opcoes={opcoesObras}
                  valor={novoSetor.obraId}
                  aoMudar={(id) => setNovoSetor({ ...novoSetor, obraId: id })}
                />
              </Field>
            </Campo>
            <Campo span={6}>
              <Field label="Nome do setor">
                <Input value={novoSetor.nome} onChange={(_, d) => setNovoSetor({ ...novoSetor, nome: d.value })} />
              </Field>
            </Campo>
          </FormGrid>
          <FormRodape>
            <Button onClick={fecharPainel}>Cancelar</Button>
            <Button appearance="primary" onClick={criar} disabled={carregando}>
              Adicionar setor
            </Button>
          </FormRodape>
        </FormSection>
      </PainelCriacaoInline>
      <Card>
        <DataTable
          aria-label="Setores cadastrados"
          colunas={colunas}
          linhas={setores}
          chaveLinha={(s) => s.id}
          carregando={carregandoLista}
          vazio={{
            titulo: 'Nenhum setor cadastrado ainda',
            acao: { rotulo: 'Adicionar setor', aoClicar: () => painel.abrir() },
          }}
          acoesLinha={(s) => (
            <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluir(s.id)} aria-label="Excluir" />
          )}
        />
      </Card>
    </div>
  );
}
```

(`criar()` já valida `!novoSetor.obraId` e já chama `fecharPainel()` no final — nenhuma mudança necessária.)

- [ ] **Step 5: Checar tipo e lint**

Run: `npx tsc -b --noEmit && npx oxlint`
Expected: sem erros.

- [ ] **Step 6: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/pages/pessoas/SetoresTab.tsx
git commit -m "feat(pessoas): migra Novo setor do PainelLateral para PainelCriacaoInline"
```

---

## Task 5: Migrar `CursosTreinamentoTab.tsx`

**Files:**
- Modify: `src/pages/pessoas/CursosTreinamentoTab.tsx`

**Interfaces:**
- Consumes: `PainelCriacaoInline`, `usePainelCriacaoInline` (Task 1). Sem campos relacionais — não usa `valorUnico()`.
- Produces: nada consumido por outra task.

- [ ] **Step 1: Trocar os imports**

Substituir as linhas 1-19 por:

```tsx
import { useEffect, useState } from 'react';
import {
  Button,
  Field,
  Input,
  Textarea,
  Card,
  PageHeader,
  DataTable,
  PainelCriacaoInline,
  usePainelCriacaoInline,
  FormSection,
  FormGrid,
  FormRodape,
  Campo,
  FeedbackInline,
  useConfirmar,
  type Coluna,
} from '@ui';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import { api, type CursoTreinamento, type NovoCursoTreinamento } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';
```

- [ ] **Step 2: Trocar `useState`/comentário**

Substituir:

```tsx
// Camada ui/ (Onda 2, Task 1): formulário de criação foi para um PainelLateral, mesmo padrão dos
// demais cadastros deste módulo.
export function CursosTreinamentoTab() {
  const [cursos, setCursos] = useState<CursoTreinamento[]>([]);
  const [novoCurso, setNovoCurso] = useState<NovoCursoTreinamento>(cursoVazio);
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [painelAberto, setPainelAberto] = useState(false);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();
```

por:

```tsx
// Onda B do spec de formulário inline (2026-09-11): PainelLateral -> PainelCriacaoInline.
export function CursosTreinamentoTab() {
  const [cursos, setCursos] = useState<CursoTreinamento[]>([]);
  const [novoCurso, setNovoCurso] = useState<NovoCursoTreinamento>(cursoVazio);
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const painel = usePainelCriacaoInline();
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();
```

- [ ] **Step 3: Atualizar `fecharPainel`**

Substituir:

```tsx
  function fecharPainel() {
    setPainelAberto(false);
    setErroPainel(null);
  }
```

por:

```tsx
  function fecharPainel() {
    painel.fechar();
    setErroPainel(null);
  }
```

- [ ] **Step 4: Atualizar o `PageHeader`, o estado vazio e trocar `PainelLateral` por `PainelCriacaoInline`**

Substituir todo o bloco do `return` por:

```tsx
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {dialogElement}
      <PageHeader
        titulo="Cursos de treinamento (catálogo)"
        acoes={
          <Button
            appearance="primary"
            icon={<Add24Regular />}
            onClick={() => (painel.aberto ? fecharPainel() : painel.abrir())}
            aria-expanded={painel.aberto}
            aria-controls={painel.id}
          >
            {painel.aberto ? 'Fechar' : 'Adicionar curso'}
          </Button>
        }
      />
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}
      <PainelCriacaoInline aberto={painel.aberto} id={painel.id} titulo="Novo curso de treinamento">
        <FormSection titulo="Dados do curso" numero={1} primeira>
          {erroPainel && (
            <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
              {erroPainel}
            </FeedbackInline>
          )}
          <FormGrid>
            <Campo span={6}>
              <Field label="Nome">
                <Input value={novoCurso.nome} onChange={(_, d) => setNovoCurso({ ...novoCurso, nome: d.value })} />
              </Field>
            </Campo>
            <Campo span={6}>
              <Field label="Norma de referência">
                <Input
                  value={novoCurso.normaReferencia ?? ''}
                  onChange={(_, d) => setNovoCurso({ ...novoCurso, normaReferencia: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={6}>
              <Field label="Carga horária mínima (h)">
                <Input
                  type="number"
                  value={String(novoCurso.cargaHorariaMinima)}
                  onChange={(_, d) => setNovoCurso({ ...novoCurso, cargaHorariaMinima: Number(d.value) })}
                />
              </Field>
            </Campo>
            <Campo span={6}>
              <Field label="Validade (meses)">
                <Input
                  type="number"
                  value={String(novoCurso.validadeEmMeses)}
                  onChange={(_, d) => setNovoCurso({ ...novoCurso, validadeEmMeses: Number(d.value) })}
                />
              </Field>
            </Campo>
            <Campo span={12}>
              <Field label="Conteúdo programático (um tópico por linha — vira o verso do certificado)">
                <Textarea
                  rows={6}
                  value={novoCurso.conteudoProgramatico ?? ''}
                  onChange={(_, d) => setNovoCurso({ ...novoCurso, conteudoProgramatico: d.value })}
                />
              </Field>
            </Campo>
          </FormGrid>
          <FormRodape>
            <Button onClick={fecharPainel}>Cancelar</Button>
            <Button appearance="primary" onClick={criar} disabled={carregando}>
              Adicionar curso
            </Button>
          </FormRodape>
        </FormSection>
      </PainelCriacaoInline>
      <Card>
        <DataTable
          aria-label="Cursos de treinamento"
          colunas={colunas}
          linhas={cursos}
          chaveLinha={(c) => c.id}
          carregando={carregandoLista}
          vazio={{
            titulo: 'Nenhum curso de treinamento cadastrado ainda',
            acao: { rotulo: 'Adicionar curso', aoClicar: () => painel.abrir() },
          }}
          acoesLinha={(c) => (
            <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluir(c.id)} aria-label="Excluir" />
          )}
        />
      </Card>
    </div>
  );
}
```

- [ ] **Step 5: Checar tipo e lint**

Run: `npx tsc -b --noEmit && npx oxlint`
Expected: sem erros.

- [ ] **Step 6: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/pages/pessoas/CursosTreinamentoTab.tsx
git commit -m "feat(pessoas): migra Novo curso de treinamento do PainelLateral para PainelCriacaoInline"
```

---

## Task 6: Migrar `TreinamentosTab.tsx`

**Files:**
- Modify: `src/pages/pessoas/TreinamentosTab.tsx`

**Interfaces:**
- Consumes: `PainelCriacaoInline`, `usePainelCriacaoInline` (Task 1); `hoje`, `valorUnico` de `../../lib/valoresPadrao` (Onda A).
- Produces: nada consumido por outra task.

**Atenção:** esta tela é sub-aba de `TrabalhadorDetalhePage` (recebe `trabalhadorId`/`obraId` via props), usa `Card acoes` em vez de `PageHeader`, tem um `useEffect` que reresta o form quando `trabalhadorId` muda, e encadeia `AssinaturaCertificadoTreinamentoDialog` após criar com sucesso — todos esses pontos devem ficar intactos.

- [ ] **Step 1: Trocar os imports**

Substituir as linhas 1-25 por:

```tsx
import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Button,
  Field,
  Input,
  CampoData,
  Card,
  DataTable,
  PainelCriacaoInline,
  usePainelCriacaoInline,
  FormSection,
  FormGrid,
  FormRodape,
  Campo,
  FeedbackInline,
  SeletorPesquisavel,
  StatusChip,
  nivelVencimento,
  tomDeVencimento,
  rotuloDeVencimento,
  useConfirmar,
  type Coluna,
} from '@ui';
import { Add24Regular, ArrowDownload24Regular, Delete24Regular, Signature24Regular } from '@fluentui/react-icons';
import { api, type CursoTreinamento, type NovoTreinamento, type Treinamento } from '../../lib/api';
import { hoje, valorUnico } from '../../lib/valoresPadrao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { AssinaturaCertificadoTreinamentoDialog } from '../../components/assinatura/AssinaturaCertificadoTreinamentoDialog';
```

- [ ] **Step 2: Pré-preencher Data de realização com `hoje()` em `treinamentoVazio`**

Substituir:

```tsx
function treinamentoVazio(trabalhadorId: string): NovoTreinamento {
  return {
    trabalhadorId,
    cursoTreinamentoId: '',
    dataRealizacao: '',
    dataValidade: '',
    cargaHorariaRealizada: 0,
    instituicaoInstrutor: '',
    numeroCertificado: '',
    local: '',
    instrutorRegistroProfissional: '',
  };
}
```

por:

```tsx
function treinamentoVazio(trabalhadorId: string): NovoTreinamento {
  return {
    trabalhadorId,
    cursoTreinamentoId: '',
    // Data de realização de um treinamento cadastrado ao vivo tende a ser hoje (spec §2) — Validade
    // fica vazia, é calculada a partir do curso, não deve ser pré-preenchida.
    dataRealizacao: hoje(),
    dataValidade: '',
    cargaHorariaRealizada: 0,
    instituicaoInstrutor: '',
    numeroCertificado: '',
    local: '',
    instrutorRegistroProfissional: '',
  };
}
```

- [ ] **Step 3: Trocar `useState`/comentário e pré-selecionar Curso único em `carregar()`**

Substituir:

```tsx
// Sub-aba de TrabalhadorDetalhePage (aba "Treinamentos & DDS"). Camada ui/ (Onda 2, Task 1): Card com
// título de seção + botão "Adicionar treinamento" no `acoes` do Card (não PageHeader — conteúdo
// aninhado); formulário de criação foi para PainelLateral. A situação de vencimento (antes calculada
// à mão em situacaoTreinamento(), mesma regra de 30 dias) passa a usar os helpers
// nivelVencimento/tomDeVencimento/rotuloDeVencimento de @ui (guia de conversão, "Badge→StatusChip"),
// mantendo a data crua ao lado do chip (aprendizado do piloto 1: chip nunca substitui sozinho um
// valor de auditoria). obraId propaga pro AssinaturaCertificadoTreinamentoDialog (AssinaturaQuiosque).
export function TreinamentosTab({ trabalhadorId, obraId }: { trabalhadorId: string; obraId: string }) {
  const navigate = useNavigate();
  const [treinamentos, setTreinamentos] = useState<Treinamento[]>([]);
  const [cursos, setCursos] = useState<CursoTreinamento[]>([]);
  const [novoTreinamento, setNovoTreinamento] = useState<NovoTreinamento>(() => treinamentoVazio(trabalhadorId));
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [painelAberto, setPainelAberto] = useState(false);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();
  const [baixandoId, setBaixandoId] = useState<string | null>(null);
  const [assinaturaAberta, setAssinaturaAberta] = useState<{
    treinamentoId: string;
    cursoNome: string;
    dataRealizacao: string;
    cargaHorariaRealizada: number;
  } | null>(null);

  async function carregar() {
    try {
      setErro(null);
      const [lista, listaCursos] = await Promise.all([
        api.treinamentos.listar(trabalhadorId),
        api.cursosTreinamento.listar(),
      ]);
      setTreinamentos(lista);
      setCursos(listaCursos);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar treinamentos.');
    } finally {
      setCarregandoLista(false);
    }
  }
```

por:

```tsx
// Sub-aba de TrabalhadorDetalhePage (aba "Treinamentos & DDS"). Onda B do spec de formulário inline
// (2026-09-11): Card com título de seção + botão "Adicionar treinamento" no `acoes` do Card (não
// PageHeader — conteúdo aninhado); formulário de criação vai para PainelCriacaoInline. A situação de
// vencimento usa os helpers nivelVencimento/tomDeVencimento/rotuloDeVencimento de @ui, mantendo a
// data crua ao lado do chip. obraId propaga pro AssinaturaCertificadoTreinamentoDialog.
export function TreinamentosTab({ trabalhadorId, obraId }: { trabalhadorId: string; obraId: string }) {
  const navigate = useNavigate();
  const [treinamentos, setTreinamentos] = useState<Treinamento[]>([]);
  const [cursos, setCursos] = useState<CursoTreinamento[]>([]);
  const [novoTreinamento, setNovoTreinamento] = useState<NovoTreinamento>(() => treinamentoVazio(trabalhadorId));
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const painel = usePainelCriacaoInline();
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();
  const [baixandoId, setBaixandoId] = useState<string | null>(null);
  const [assinaturaAberta, setAssinaturaAberta] = useState<{
    treinamentoId: string;
    cursoNome: string;
    dataRealizacao: string;
    cargaHorariaRealizada: number;
  } | null>(null);

  async function carregar() {
    try {
      setErro(null);
      const [lista, listaCursos] = await Promise.all([
        api.treinamentos.listar(trabalhadorId),
        api.cursosTreinamento.listar(),
      ]);
      setTreinamentos(lista);
      setCursos(listaCursos);
      // Select com uma única opção possível no contexto atual vem pré-selecionado (spec §2). Não
      // sobrescreve se o usuário já tiver escolhido um Curso.
      setNovoTreinamento((prev) =>
        prev.cursoTreinamentoId ? prev : { ...prev, cursoTreinamentoId: valorUnico(listaCursos, (c) => c.id) },
      );
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar treinamentos.');
    } finally {
      setCarregandoLista(false);
    }
  }
```

- [ ] **Step 4: Preservar o `useEffect` de reset por `trabalhadorId` e atualizar `fecharPainel`**

Substituir:

```tsx
  useEffect(() => {
    carregar();
    setNovoTreinamento(treinamentoVazio(trabalhadorId));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [trabalhadorId]);

  function fecharPainel() {
    setPainelAberto(false);
    setErroPainel(null);
  }
```

por:

```tsx
  useEffect(() => {
    carregar();
    setNovoTreinamento(treinamentoVazio(trabalhadorId));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [trabalhadorId]);

  function fecharPainel() {
    painel.fechar();
    setErroPainel(null);
  }
```

(`treinamentoVazio(trabalhadorId)` já chama `hoje()` internamente pelo Step 2 — o reset ao trocar de trabalhador continua pré-preenchendo a data.)

- [ ] **Step 5: Atualizar o `Card acoes`, o estado vazio e trocar `PainelLateral` por `PainelCriacaoInline`**

Substituir todo o bloco do `return` (da abertura `return (` até o `);` final, incluindo o `AssinaturaCertificadoTreinamentoDialog`) por:

```tsx
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {dialogElement}
      <Card
        titulo="Treinamentos do funcionário"
        acoes={
          <Button
            appearance="primary"
            icon={<Add24Regular />}
            onClick={() => (painel.aberto ? fecharPainel() : painel.abrir())}
            aria-expanded={painel.aberto}
            aria-controls={painel.id}
          >
            {painel.aberto ? 'Fechar' : 'Adicionar treinamento'}
          </Button>
        }
      >
        {erro && (
          <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
            {erro}
          </FeedbackInline>
        )}
        <DataTable
          aria-label="Treinamentos do funcionário"
          colunas={colunas}
          linhas={treinamentos}
          chaveLinha={(t) => t.id}
          carregando={carregandoLista}
          vazio={{
            titulo: 'Nenhum treinamento cadastrado ainda',
            acao: { rotulo: 'Adicionar treinamento', aoClicar: () => painel.abrir() },
          }}
          acoesLinha={(t) => (
            <>
              <Button
                appearance="subtle"
                size="small"
                icon={<Signature24Regular />}
                onClick={() => navigate(`/treinamentos/${t.id}/assinar`)}
                aria-label="Assinar certificado"
                title="Assinar certificado"
              />
              <Button
                appearance="subtle"
                size="small"
                icon={<ArrowDownload24Regular />}
                onClick={() => baixarCertificado(t.id)}
                disabled={baixandoId === t.id}
                aria-label="Baixar certificado"
                title="Baixar certificado em PDF"
              />
              <Button
                appearance="subtle"
                size="small"
                icon={<Delete24Regular />}
                onClick={() => excluir(t.id)}
                aria-label="Excluir"
              />
            </>
          )}
        />
      </Card>

      <PainelCriacaoInline aberto={painel.aberto} id={painel.id} titulo="Novo treinamento">
        <FormSection titulo="Dados do treinamento" numero={1} primeira>
          {erroPainel && (
            <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
              {erroPainel}
            </FeedbackInline>
          )}
          <FormGrid>
            <Campo span={12}>
              <Field label="Curso">
                <SeletorPesquisavel
                  placeholder="Selecione um curso"
                  opcaoVazia="Selecione um curso"
                  opcoes={opcoesCursos}
                  valor={novoTreinamento.cursoTreinamentoId}
                  aoMudar={(id) => setNovoTreinamento({ ...novoTreinamento, cursoTreinamentoId: id })}
                />
              </Field>
            </Campo>
            <Campo span={6}>
              <Field label="Data de realização">
                <CampoData
                  value={novoTreinamento.dataRealizacao}
                  onChange={(_, d) => setNovoTreinamento({ ...novoTreinamento, dataRealizacao: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={6}>
              <Field label="Validade">
                <CampoData
                  value={novoTreinamento.dataValidade}
                  onChange={(_, d) => setNovoTreinamento({ ...novoTreinamento, dataValidade: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={6}>
              <Field label="Carga horária realizada (h)">
                <Input
                  type="number"
                  value={String(novoTreinamento.cargaHorariaRealizada)}
                  onChange={(_, d) => setNovoTreinamento({ ...novoTreinamento, cargaHorariaRealizada: Number(d.value) })}
                />
              </Field>
            </Campo>
            <Campo span={6}>
              <Field label="Número do certificado">
                <Input
                  value={novoTreinamento.numeroCertificado ?? ''}
                  onChange={(_, d) => setNovoTreinamento({ ...novoTreinamento, numeroCertificado: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={6}>
              <Field label="Técnico de Segurança do Trabalho (Instrutor/Resp. Técnico)">
                <Input
                  value={novoTreinamento.instituicaoInstrutor ?? ''}
                  onChange={(_, d) => setNovoTreinamento({ ...novoTreinamento, instituicaoInstrutor: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={6}>
              <Field label="Registro profissional do instrutor (CREA/MTE)">
                <Input
                  value={novoTreinamento.instrutorRegistroProfissional ?? ''}
                  onChange={(_, d) => setNovoTreinamento({ ...novoTreinamento, instrutorRegistroProfissional: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={12}>
              <Field label="Local / Instalações (opcional — sem preencher, usa a Obra)">
                <Input
                  value={novoTreinamento.local ?? ''}
                  onChange={(_, d) => setNovoTreinamento({ ...novoTreinamento, local: d.value })}
                />
              </Field>
            </Campo>
          </FormGrid>
          <FormRodape>
            <Button onClick={fecharPainel}>Cancelar</Button>
            <Button appearance="primary" onClick={criar} disabled={carregando}>
              Adicionar treinamento
            </Button>
          </FormRodape>
        </FormSection>
      </PainelCriacaoInline>

      {assinaturaAberta && (
        <AssinaturaCertificadoTreinamentoDialog
          open
          onClose={() => setAssinaturaAberta(null)}
          treinamentoId={assinaturaAberta.treinamentoId}
          cursoNome={assinaturaAberta.cursoNome}
          dataRealizacao={assinaturaAberta.dataRealizacao}
          cargaHorariaRealizada={assinaturaAberta.cargaHorariaRealizada}
          obraId={obraId}
        />
      )}
    </div>
  );
}
```

(`criar()` não muda — continua disparando `setAssinaturaAberta(...)` e chamando `fecharPainel()` no final, exatamente como antes.)

- [ ] **Step 6: Checar tipo e lint**

Run: `npx tsc -b --noEmit && npx oxlint`
Expected: sem erros.

- [ ] **Step 7: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/pages/pessoas/TreinamentosTab.tsx
git commit -m "feat(pessoas): migra Novo treinamento do PainelLateral para PainelCriacaoInline"
```

---

## Task 7: Migrar `TrabalhadoresTab.tsx`

**Files:**
- Modify: `src/pages/pessoas/TrabalhadoresTab.tsx`

**Interfaces:**
- Consumes: `PainelCriacaoInline`, `usePainelCriacaoInline` (Task 1); `valorUnico` (Onda A).
- Produces: nada consumido por outra task.

**Atenção — 3 particularidades desta tela (a mais complexa das 6):**
1. `largura="lg"` do `PainelLateral` não tem equivalente em `PainelCriacaoInline` — é descartada (o painel inline cresce na largura total da coluna da página).
2. `fecharPainel()` hoje reseta `novoTrabalhador` além de `painelAberto`/`erroPainel`, e `criar()` NÃO chama `fecharPainel()` (duplica `setPainelAberto(false)` direto) — diferente do padrão canônico das outras 5 telas. Esta task nivela o padrão: `fecharPainel()` só cuida do painel/erro; o reset do formulário e o fechamento em `criar()` passam a seguir o mesmo molde das outras telas (reset + `fecharPainel()` no final do try).
3. Ao criar com sucesso, `criar()` dispara `setTrabalhadorRequisitosAlvo(...)`, que depois abre `CadastroDigitalDialog` — esse encadeamento continua intacto.

- [ ] **Step 1: Trocar os imports**

Substituir as linhas 1-40 por:

```tsx
import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Avatar,
  Button,
  Card,
  PageHeader,
  DataTable,
  StatusChip,
  FeedbackInline,
  PainelCriacaoInline,
  usePainelCriacaoInline,
  FormSection,
  FormGrid,
  FormRodape,
  Campo,
  Field,
  Input,
  Select,
  CampoData,
  useConfirmar,
  type Coluna,
  type Tom,
} from '@ui';
import { Add24Regular, Fingerprint24Regular, Search24Regular } from '@fluentui/react-icons';
import { SeletorFotoCamera } from '../../components/SeletorFotoCamera';
import {
  api,
  resultadoAsoLabel,
  ResultadoAso,
  tipoVinculoLabel,
  TipoVinculo,
  type Aso,
  type Funcao,
  type NovoTrabalhador,
  type Obra,
  type Trabalhador,
} from '../../lib/api';
import { formatarCpf } from '../../lib/cpf';
import { valorUnico } from '../../lib/valoresPadrao';
import { CadastroDigitalDialog } from '../../components/pessoas/CadastroDigitalDialog';
import { RequisitosFuncaoDialog } from '../../components/pessoas/RequisitosFuncaoDialog';
import { useSucessoToast } from '../../hooks/useSucessoToast';
```

- [ ] **Step 2: Trocar `useState`/comentário, e pré-selecionar Obra e Função únicas em `carregar()`**

Substituir:

```tsx
// Lista de funcionários — piloto 1 estabeleceu o padrão (spec §4.2): o formulário de cadastro sai da
// lista (que hoje empurrava a tabela pra baixo) e vira um PainelLateral próprio, com erro isolado do
// erro de carga da lista (regra dos 3 pilotos: erro de painel é estado PRÓPRIO). A lista em si já era
// um cartão-por-linha construído à mão — vira DataTable, que formaliza exatamente esse visual.
export function TrabalhadoresTab() {
  const navigate = useNavigate();
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [funcoes, setFuncoes] = useState<Funcao[]>([]);
  const [asos, setAsos] = useState<Aso[]>([]);
  const [busca, setBusca] = useState('');
  const [novoTrabalhador, setNovoTrabalhador] = useState<NovoTrabalhador>(trabalhadorVazio);
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [painelAberto, setPainelAberto] = useState(false);
  const [fotoUrls, setFotoUrls] = useState<Record<string, string>>({});
  const [trabalhadorDigitalAlvo, setTrabalhadorDigitalAlvo] = useState<{ id: string; nome: string } | null>(
    null,
  );
  const [trabalhadorRequisitosAlvo, setTrabalhadorRequisitosAlvo] = useState<{
    id: string;
    nome: string;
    funcaoId: string;
  } | null>(null);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [trabs, obs, funcs, asosResp] = await Promise.all([
        api.trabalhadores.listar(),
        api.obras.listar(),
        api.funcoes.listar(),
        api.asos.listar(),
      ]);
      setTrabalhadores(trabs);
      setObras(obs);
      setFuncoes(funcs);
      setAsos(asosResp);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar funcionários.');
    } finally {
      setCarregandoLista(false);
    }
  }
```

por:

```tsx
// Lista de funcionários. Onda B do spec de formulário inline (2026-09-11): PainelLateral ->
// PainelCriacaoInline, com erro isolado do erro de carga da lista (regra: erro de painel é estado
// PRÓPRIO).
export function TrabalhadoresTab() {
  const navigate = useNavigate();
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [funcoes, setFuncoes] = useState<Funcao[]>([]);
  const [asos, setAsos] = useState<Aso[]>([]);
  const [busca, setBusca] = useState('');
  const [novoTrabalhador, setNovoTrabalhador] = useState<NovoTrabalhador>(trabalhadorVazio);
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const painel = usePainelCriacaoInline();
  const [fotoUrls, setFotoUrls] = useState<Record<string, string>>({});
  const [trabalhadorDigitalAlvo, setTrabalhadorDigitalAlvo] = useState<{ id: string; nome: string } | null>(
    null,
  );
  const [trabalhadorRequisitosAlvo, setTrabalhadorRequisitosAlvo] = useState<{
    id: string;
    nome: string;
    funcaoId: string;
  } | null>(null);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [trabs, obs, funcs, asosResp] = await Promise.all([
        api.trabalhadores.listar(),
        api.obras.listar(),
        api.funcoes.listar(),
        api.asos.listar(),
      ]);
      setTrabalhadores(trabs);
      setObras(obs);
      setFuncoes(funcs);
      setAsos(asosResp);
      // Select com uma única opção possível no contexto atual vem pré-selecionado (spec §2). Não
      // sobrescreve se o usuário já tiver escolhido Obra/Função.
      setNovoTrabalhador((prev) => ({
        ...prev,
        obraId: prev.obraId ? prev.obraId : valorUnico(obs, (o) => o.id),
        funcaoId: prev.funcaoId ? prev.funcaoId : valorUnico(funcs, (f) => f.id),
      }));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar funcionários.');
    } finally {
      setCarregandoLista(false);
    }
  }
```

- [ ] **Step 3: Nivelar `fecharPainel` e `criar()` ao padrão canônico (reset do form dentro de `criar()`, `fecharPainel()` só cuida do painel/erro)**

Substituir:

```tsx
  // Todo caminho de fechar o painel limpa o formulário e o erro dele — senão reabrir mostra rascunho
  // e mensagem de uma tentativa anterior.
  function fecharPainel() {
    setPainelAberto(false);
    setErroPainel(null);
    setNovoTrabalhador(trabalhadorVazio);
  }

  async function criar() {
    try {
      setCarregando(true);
      setErroPainel(null);
      const { id } = await api.trabalhadores.criar(novoTrabalhador);
      setTrabalhadorRequisitosAlvo({ id, nome: novoTrabalhador.nome, funcaoId: novoTrabalhador.funcaoId });
      setNovoTrabalhador(trabalhadorVazio);
      setPainelAberto(false);
      await carregar();
      sucessoToast('Funcionário criado com sucesso.');
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao criar funcionário.');
    } finally {
      setCarregando(false);
    }
  }
```

por:

```tsx
  function fecharPainel() {
    painel.fechar();
    setErroPainel(null);
  }

  async function criar() {
    try {
      setCarregando(true);
      setErroPainel(null);
      const { id } = await api.trabalhadores.criar(novoTrabalhador);
      setTrabalhadorRequisitosAlvo({ id, nome: novoTrabalhador.nome, funcaoId: novoTrabalhador.funcaoId });
      setNovoTrabalhador(trabalhadorVazio);
      await carregar();
      sucessoToast('Funcionário criado com sucesso.');
      fecharPainel();
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao criar funcionário.');
    } finally {
      setCarregando(false);
    }
  }
```

(Nota: `carregar()` roda logo antes de `fecharPainel()` e já reaplica `valorUnico()` em cima do `trabalhadorVazio` recém-setado — então reabrir o painel continua vindo com Obra/Função pré-selecionadas quando só há 1 opção, igual ao comportamento anterior a esta mudança.)

- [ ] **Step 4: Atualizar o `PageHeader`, o estado vazio, remover `largura="lg"` e trocar `PainelLateral` por `PainelCriacaoInline`**

Substituir o `PageHeader` (linhas 262-274 do arquivo original):

```tsx
      <PageHeader
        titulo="Funcionários cadastrados"
        filtros={
          <Field label="Buscar por nome ou matrícula">
            <Input contentBefore={<Search24Regular />} value={busca} onChange={(_, d) => setBusca(d.value)} />
          </Field>
        }
        acoes={
          <Button appearance="primary" icon={<Add24Regular />} onClick={() => setPainelAberto(true)}>
            Adicionar funcionário
          </Button>
        }
      />
```

por:

```tsx
      <PageHeader
        titulo="Funcionários cadastrados"
        filtros={
          <Field label="Buscar por nome ou matrícula">
            <Input contentBefore={<Search24Regular />} value={busca} onChange={(_, d) => setBusca(d.value)} />
          </Field>
        }
        acoes={
          <Button
            appearance="primary"
            icon={<Add24Regular />}
            onClick={() => (painel.aberto ? fecharPainel() : painel.abrir())}
            aria-expanded={painel.aberto}
            aria-controls={painel.id}
          >
            {painel.aberto ? 'Fechar' : 'Adicionar funcionário'}
          </Button>
        }
      />
```

O `vazio.acao.aoClicar` da `DataTable` (hoje `() => setPainelAberto(true)`, dentro do ramo `trabalhadores.length === 0` do `vazio` condicional — o outro ramo, "Nenhum funcionário encontrado", não tem `acao`) já é coberto pelo bloco final abaixo, que substitui o `<Card>` inteiro — não é preciso editá-lo à parte.

Substituir o bloco `<PainelLateral ...>...</PainelLateral>` (linhas 325-427 do arquivo original) **e** o `<Card>` da lista (linhas 278-323) — reposicionando o painel **antes** do `<Card>`, como nas outras 5 telas — pelo bloco único abaixo:

```tsx
      <PainelCriacaoInline aberto={painel.aberto} id={painel.id} titulo="Novo funcionário">
        <FormSection titulo="Dados do funcionário" numero={1} primeira>
          {erroPainel && (
            <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
              {erroPainel}
            </FeedbackInline>
          )}
          <FormGrid>
            <Campo span={6}>
              <Field label="Obra">
                <Select
                  value={novoTrabalhador.obraId}
                  onChange={(_, d) => setNovoTrabalhador({ ...novoTrabalhador, obraId: d.value })}
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
            <Campo span={6}>
              <Field label="Função">
                <Select
                  value={novoTrabalhador.funcaoId}
                  onChange={(_, d) => setNovoTrabalhador({ ...novoTrabalhador, funcaoId: d.value })}
                >
                  <option value="">Selecione</option>
                  {funcoes.map((funcao) => (
                    <option key={funcao.id} value={funcao.id}>
                      {funcao.nome}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={8}>
              <Field label="Nome">
                <Input
                  value={novoTrabalhador.nome}
                  onChange={(_, d) => setNovoTrabalhador({ ...novoTrabalhador, nome: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={4}>
              <Field label="Matrícula">
                <Input
                  value={novoTrabalhador.matricula ?? ''}
                  onChange={(_, d) => setNovoTrabalhador({ ...novoTrabalhador, matricula: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={6}>
              <Field label="CPF (11 dígitos)">
                <Input
                  value={formatarCpf(novoTrabalhador.cpf)}
                  onChange={(_, d) =>
                    setNovoTrabalhador({ ...novoTrabalhador, cpf: d.value.replace(/\D/g, '').slice(0, 11) })
                  }
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Vínculo">
                <Select
                  value={novoTrabalhador.vinculo}
                  onChange={(_, d) => setNovoTrabalhador({ ...novoTrabalhador, vinculo: Number(d.value) })}
                >
                  {Object.entries(tipoVinculoLabel).map(([valor, rotulo]) => (
                    <option key={valor} value={valor}>
                      {rotulo}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Data de admissão">
                <CampoData
                  value={novoTrabalhador.dataAdmissao}
                  onChange={(_, d) => setNovoTrabalhador({ ...novoTrabalhador, dataAdmissao: d.value })}
                />
              </Field>
            </Campo>
          </FormGrid>
          <FormRodape>
            <Button onClick={fecharPainel}>Cancelar</Button>
            <Button appearance="primary" onClick={criar} disabled={carregando}>
              Adicionar funcionário
            </Button>
          </FormRodape>
        </FormSection>
      </PainelCriacaoInline>

      <Card>
        <DataTable
          aria-label="Funcionários cadastrados"
          colunas={colunas}
          linhas={trabalhadoresFiltrados}
          chaveLinha={(t) => t.id}
          carregando={carregandoLista}
          aoClicarLinha={(t) => navigate(`/pessoas/${t.id}`)}
          vazio={
            trabalhadores.length === 0
              ? {
                  titulo: 'Nenhum funcionário cadastrado ainda.',
                  descricao: 'Cadastre o primeiro funcionário para começar.',
                  acao: { rotulo: 'Adicionar funcionário', aoClicar: () => painel.abrir() },
                }
              : { titulo: 'Nenhum funcionário encontrado.', descricao: 'Tente outro termo de busca.', variante: 'sem-resultado' }
          }
          acoesLinha={(t) => (
            <div style={{ display: 'flex', gap: 4 }}>
              <Button
                appearance="subtle"
                size="small"
                icon={<Fingerprint24Regular />}
                onClick={(evento) => {
                  evento.stopPropagation();
                  setTrabalhadorDigitalAlvo({ id: t.id, nome: t.nome });
                }}
                aria-label="Cadastrar digital"
                title="Cadastrar digital"
              />
              <span onClick={(evento) => evento.stopPropagation()}>
                <SeletorFotoCamera
                  rotulo="Enviar foto"
                  apenasIcone
                  tiposAceitos="image/png,image/jpeg"
                  aoSelecionarArquivo={(arquivo) => enviarFoto(t.id, arquivo)}
                  aoErroValidacao={setErro}
                />
              </span>
              <Button appearance="subtle" size="small" onClick={() => excluir(t.id)} aria-label="Excluir">
                Excluir
              </Button>
            </div>
          )}
        />
      </Card>
```

Este bloco substitui, de uma vez, tanto o `<Card>` original (linhas 278-323) quanto o `<PainelLateral>` original (linhas 325-427), na nova ordem (painel antes da lista). O restante do arquivo (comentário do `return`/`<div>` de abertura, `{dialogElement}`, e os dois `<RequisitosFuncaoDialog>`/`<CadastroDigitalDialog>` no fim) não muda de conteúdo — só envolva o `<div>` de abertura do `return` com o container flex, igual às outras 5 telas:

- [ ] **Step 5: Envolver o `return` num container com espaçamento**

Trocar a abertura do `return`:

```tsx
  return (
    <div>
      {dialogElement}
```

por:

```tsx
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {dialogElement}
```

- [ ] **Step 6: Checar tipo e lint**

Run: `npx tsc -b --noEmit && npx oxlint`
Expected: sem erros. Confira que nenhum import de `PainelLateral` ficou sobrando.

- [ ] **Step 7: Commit**

```bash
git add src/AAHBRANT.SST.TeamsApp/src/pages/pessoas/TrabalhadoresTab.tsx
git commit -m "feat(pessoas): migra Novo funcionario do PainelLateral para PainelCriacaoInline"
```

---

## Task 8: Verificação visual manual (desktop + tablet), 6 telas

**Files:** nenhum (só verificação — não edita código).

**Interfaces:**
- Consumes: app rodando via preview (dev server), rota de Pessoas.

- [ ] **Step 1: Subir o preview do app** (dev server do projeto).

- [ ] **Step 2: Para cada uma das 6 telas (Pessoas → Funcionários / Funções / Equipes / Setores / Cursos; e Pessoas → [um funcionário] → Treinamentos), em viewport desktop:**
  - Clicar no botão "Adicionar X" e confirmar: o formulário aparece acima da tabela, sem vão vazio, com "Etapa 1 - Dados de..." visível; clicar de novo fecha (toggle); "Cancelar" fecha sem enviar.
  - Se houver só 1 Obra/Setor/Curso nos dados de teste, confirmar que o campo relacional correspondente já vem pré-selecionado (Obra em Trabalhadores/Setores; Setor em Equipes; Curso em Treinamentos). Se houver mais de uma opção, essa regra é esperada **não** disparar — não é bug.
  - Em Treinamentos, confirmar que "Data de realização" já vem preenchida com a data de hoje ao abrir o painel.

- [ ] **Step 3: Repetir em viewport tablet** (ex.: 834×1194) para as 6 telas e confirmar que não sobra espaço vazio entre os campos e o rodapé.

- [ ] **Step 4: Reportar ao usuário** com pelo menos uma captura de tela (desktop e tablet) antes de considerar a Onda B concluída — nenhum deploy é feito nesta tarefa.

---

## Self-Review Notes (preenchido durante a escrita do plano)

- **Cobertura do spec:** Task 1 cobre a seção 5 (addendum) do spec — `id`/`subtitulo`/hook. Tasks 2-7 cobrem as 6 telas da Onda B (seção 3). Task 8 cobre a verificação por onda exigida pela seção 3.
- **Placeholders:** nenhum "TBD" — todo passo tem código completo, inclusive os 3 pontos de atenção específicos de `TrabalhadoresTab.tsx` (largura descartada, nivelamento de `fecharPainel`/`criar()`, encadeamento de dialogs preservado).
- **Consistência de tipos:** `PainelCriacaoInlineProps` (Task 1: `aberto`, `titulo`, `subtitulo?`, `id?`, `children`) é o mesmo usado em todas as Tasks 2-7. `PainelCriacaoInlineControle` (Task 1: `aberto`, `id`, `abrir`, `fechar`, `alternar`) é o mesmo consumido via `painel.*` em todas as 6 migrações.
- **`TrabalhadoresGaveta.tsx`:** confirmado fora do escopo (Global Constraints) — não aparece em nenhuma task.
- **Fora de escopo, de propósito:** as ~19 telas restantes (Onda C em diante), a auditoria de pré-preenchimento das ~40 telas já inline (Onda F), e a atualização da galeria para `id`/`subtitulo` — tudo isso fica para planos futuros.

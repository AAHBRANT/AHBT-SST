# Padronização de formulários — painel lateral → inline — Design

**Data:** 2026-09-11
**Status:** Aprovado pelo usuário em brainstorming (2026-09-11), pronto para plano de implementação.
**Substitui parcialmente:** a decisão de convivência de dois padrões registrada em
`docs/superpowers/specs/2026-09-07-sistema-de-design-design.md` (seção "Compostos" — `PainelLateral`) e
resumida em `docs/design-system.md` §5. Este documento não invalida o resto da spec de 07/09 (tokens,
tipografia, `FormSection`/`FormGrid`, etc. continuam valendo) — só o critério de quando usar
`PainelLateral` vs. formulário inline.

## Contexto e objetivo

O usuário reportou, mostrando dois prints, que o painel lateral ("Nova atividade", em
`pages/riscos/AtividadesTab.tsx`) fica ruim em tablet: o drawer estica até a altura total da tela, mas o
formulário tem só 3 campos curtos, sobrando um vão vazio enorme antes dos botões de rodapé — pior ainda
em telas mais estreitas/proporcionalmente mais altas. O padrão que o usuário quer generalizar é o do 2º
print ("Nova instalação", `pages/epc/InstalacoesTab.tsx`): formulário embutido na página, em `Card` com
`FormSection` numerada, campos já pré-preenchidos quando há um valor óbvio (a data de instalação já vem
com a data de hoje).

Levantamento do estado atual (agente de exploração, 2026-09-11):

- **Padrão A — painel lateral.** Componente único e já consolidado: `ui/compostos/PainelLateral/PainelLateral.tsx`
  (wrapper de `OverlayDrawer` do Fluent, `position="end"`, larguras `md`=440px/`lg`=640px). Usado em
  **26 telas de feature**: Pessoas (Trabalhadores, Funções, Equipes, Setores, Cursos, Treinamentos — 6),
  CIPA (Dimensionamento, Inspeções, Membros, Processo Eleitoral, Reuniões, Sipat — 6), EPC/EPI (Catálogo
  EPC, Catálogo EPI, Entregas EPI — 3), Inspeções (Checklist Modelos, Inspeções — 2), DDS (Catálogo Temas,
  DDS Semanal — 2), Alertas (Configuração, Lista — 2), Gestão de SST (Materiais de Apoio), Treinamentos
  (Turmas), Não Conformidades, Obras, Riscos/Atividades (1 cada). Nenhum uso direto de `OverlayDrawer`
  fora do wrapper — a migração não precisa caçar drawers "selvagens".
- **Padrão B — inline.** `Card` + `FormSection` (seção numerada) + `FormGrid`/`Campo` + `FormRodape`, sem
  overlay — o formulário nasce embutido na própria página, normalmente atrás de um `Select`/estado que
  precisa existir antes (ex.: escolher a Obra). Usado em **~40 arquivos**, cobrindo PGR, PT, APR, EPC/EPI
  Estoque, Administração, Saúde Ocupacional, Requisitos Legais, entre outros. Pré-preenchimento já existe
  em alguns pontos, mas via helper duplicado por arquivo (`const hoje = () => new Date().toISOString().slice(0,10)`
  aparece pelo menos em `InstalacoesTab.tsx`), não como convenção central.
- **Não existe `DatePicker` de calendário no design system** — o campo de data padrão é `CampoData`
  (`src/components/CampoData.tsx`), um `Input` de texto mascarado `dd/mm/aaaa`, criado a pedido do
  usuário por ser mais rápido de digitar que o `<input type="date">` nativo. Este design não muda isso.

## Decisão tomada com o usuário

| Decisão | Escolha | Alternativa descartada |
|---|---|---|
| Alcance da migração | Eliminar `PainelLateral` de **todas** as 26 telas, inclusive as com listas longas (Pessoas, CIPA) | Manter os dois padrões e só corrigir o comportamento do drawer em tablet (breakpoint) |
| Pré-preenchimento | Regra única de valores padrão, aplicada em **todo** formulário do sistema (migrados e já-inline) | Aplicar só nos formulários migrados nesta rodada |
| Ritmo de execução | Faseado em ondas por módulo, com verificação visual (desktop + tablet) a cada onda, alinhado ao método já usado no projeto (`docs/superpowers/plans/2026-09-08-sistema-de-design-onda-2-3.md`) | Big-bang: migrar as 27 telas em uma tacada só |
| Remoção do componente `PainelLateral` | Não remover agora — só parar de usá-lo. Decisão de apagar o arquivo fica para depois de todas as ondas, com confirmação explícita do usuário | Apagar assim que a última tela migrar |

## 1. Componente novo: painel de criação inline com toggle

Hoje cada uma das ~40 telas do Padrão B resolve "mostrar/esconder o formulário de criação" com
`useState` própria e JSX duplicado. Para não repetir esse boilerplate em mais 27 telas, entra 1 composto
novo em `ui/compostos/`:

- **Nome provisório:** `PainelCriacaoInline` (nome final em português, curto, segue a convenção de
  `PainelLateral`/`FormSection` já existentes — confirmar no code review da Onda A).
- **Responsabilidade:** um botão "+ Novo X" (ou equivalente) que revela/esconde um `Card` com
  `FormSection`/`FormGrid`/`FormRodape` dentro, posicionado **acima** da lista/tabela da página (nunca
  como overlay). Anima com `AnimatePresence` + altura/opacidade (`transicaoNormal` de
  `ui/tokens/movimento.ts`) — o mesmo mecanismo que `ui/layout/WorkflowActions/WorkflowActions.tsx` já
  usa para "formulário cresce de onde foi acionado" — para não perder a sensação de transição suave que
  o drawer tinha, seguindo o princípio de movimento já documentado (origem, não decoração).
- **O que ele NÃO faz:** não busca dado, não sabe de API — só orquestra abrir/fechar e o slot de
  conteúdo, igual à regra de dependência do resto de `src/ui/` (`ui/*` nunca importa `lib/api`).
- **Reuso:** `FormSection`, `FormGrid`, `FormRodape`, `Card` já existem e não mudam — o componente novo é
  só a casca de toggle + posicionamento + animação em volta deles.

## 2. Convenção de pré-preenchimento (regra central, não por arquivo)

Novo helper único, `lib/valoresPadrao.ts` (substitui os `hoje()` duplicados):

- Campo de data sem motivo para ficar vazio → data de hoje.
- Campo que replica um filtro/seleção já ativo na página (Obra, Empresa) → herda o valor atual.
- Campo numérico com valor óbvio (ex. Quantidade) → `1`.
- `Select` com uma única opção possível no contexto atual → vem pré-selecionado.
- Campos sem valor óbvio (Nome, Descrição, campos de texto livre) continuam vazios — pré-seleção não é
  adivinhação de dado que o usuário precisa decidir.

Esta convenção vale tanto para as 27 telas migradas quanto para as ~40 que já são inline — a Onda F
(seção 3) audita as ~40 e completa onde faltar.

## 3. Plano de ondas

| Onda | Telas | Critério de ordem |
|---|---|---|
| **A — Piloto** | `AtividadesTab` (o caso reportado) + construção de `PainelCriacaoInline` e `lib/valoresPadrao.ts` | Valida o padrão novo com o usuário antes de replicar 26 vezes |
| **B — Pessoas** | Trabalhadores, Funções, Equipes, Setores, Cursos, Treinamentos (6 telas) | Módulo de origem histórica do `PainelLateral` (nasceu de `TrabalhadoresGaveta`); maior lista real do sistema (~200 trabalhadores mock) — melhor teste de estresse para "lista longa não pode sumir" |
| **C — CIPA** | Dimensionamento, Inspeções, Membros, Processo Eleitoral, Reuniões, Sipat (6 telas) | Módulo inteiro e isolado, baixo acoplamento com o resto |
| **D — EPC/EPI/Inspeções/NC** | Catálogo EPC, Catálogo EPI, Entregas EPI, Checklist Modelos, Inspeções, Não Conformidades (6 telas) | Módulos já com exemplos irmãos no Padrão B (EstoqueTab/EstoqueEpcTab), reduz risco de padrão novo |
| **E — Restante** | DDS Catálogo Temas, DDS Semanal, Alertas Configuração, Alertas Lista, Materiais de Apoio, Turmas, Obras (7 telas) | Fecha as 26 telas (1 da Onda A + 6+6+6+7 das demais) |
| **F — Auditoria + documentação** | Conferir pré-preenchimento nas ~40 telas já inline e completar onde faltar; atualizar `docs/design-system.md` §5 e a spec de 07/09 registrando esta decisão por cima; decidir com o usuário se `PainelLateral.tsx` é removido | Fecha o padrão para o sistema inteiro, não só para as telas migradas |

**Verificação por onda:** abrir o app no preview, testar a tela em viewport desktop e tablet
(`resize_window`), conferir pré-preenchimento, só então considerar a onda pronta. Nenhuma onda é
implantada (`az acr build`/`containerapp update`) sem o usuário ver rodando localmente e confirmar —
regra já registrada no projeto (`feedback_mostrar_antes_de_deploy`, `feedback_deploy_so_quando_mandar`).

## 4. Fica fora, de propósito

- Remoção física de `PainelLateral.tsx` — decisão adiada para depois da Onda F, com o usuário
  (`feedback_nao_excluir_sem_perguntar`).
- Mudar o campo de data para um calendário de verdade — fora de escopo, `CampoData` já é a escolha
  deliberada do sistema.
- Qualquer mudança em backend/API — este design é só front-end
  (`src/AAHBRANT.SST.TeamsApp`).

## 5. Addendum pós-Onda A (2026-09-11) — hook `usePainelCriacaoInline`, não gatilho embutido

A Onda A foi implementada, revisada (SDD: 5 tasks + revisão final de branch) e implantada em hml via
PR #69. A revisão final de branch (modelo mais capaz) encontrou uma lacuna real no design original
desta seção: o componente ficou **só controlado** (`{ aberto, titulo, children }`, sem gatilho próprio),
então quem consome (`AtividadesTab.tsx`) teve que escrever à mão o `useState`, o rótulo condicional do
botão, o ícone e o `aria-expanded`/`aria-controls`. Um desses pontos manuais (o `onClick` do botão não
limpando `erroPainel` ao fechar) já nasceu com bug — corrigido ainda na Onda A, mas é exatamente o tipo
de defeito que se replicaria 25 vezes sem uma correção de design.

**Primeira decisão registrada aqui (revertida abaixo):** um `gatilho` opcional que faria o próprio
`PainelCriacaoInline` renderizar seu botão de abrir/fechar. O levantamento das 6 telas da Onda B
(Trabalhadores, Funções, Equipes, Setores, Cursos, Treinamentos) mostrou que essa forma não serve: **as
6 telas, como `AtividadesTab.tsx` na Onda A, mantêm o botão "+ Novo X" dentro do cabeçalho** (`PageHeader`
em 5 delas; dentro do `acoes` de um `Card` em `TreinamentosTab.tsx`, que é sub-aba sem `PageHeader`
próprio) — nunca solto acima do formulário. Um `gatilho` que renderiza o próprio botão do componente
nunca seria usado por nenhuma das 6 telas reais; só serviria telas hipotéticas que este projeto não tem.

**Decisão final:** em vez de um render-prop, o componente ganha 2 props opcionais e aditivas
(`aberto`/`titulo`/`children` continuam funcionando sozinhos, `AtividadesTab.tsx` não muda) e um hook
companheiro, que juntos cobrem exatamente o que se repete nas 6 telas independentemente de onde o botão
mora:

```ts
export interface PainelCriacaoInlineProps {
  aberto: boolean;
  titulo: ReactNode;
  subtitulo?: ReactNode; // repassado ao Card — cobre o caso de FuncoesTab.tsx
  id?: string;           // aplicado no wrapper; alvo de aria-controls do botão externo
  children: ReactNode;
}

// ui/compostos/PainelCriacaoInline/usePainelCriacaoInline.ts
export function usePainelCriacaoInline() {
  const [aberto, setAberto] = useState(false);
  const id = useId();
  return {
    aberto,
    id,
    abrir: () => setAberto(true),
    fechar: () => setAberto(false),
    alternar: () => setAberto((a) => !a),
  } as const;
}
```

O hook não sabe de `erroPainel` (é estado específico de cada página) — cada tela continua com seu
`fecharPainel()` local, só que ele chama `painel.fechar()` em vez de gerenciar o `useState` à mão:

```tsx
const painel = usePainelCriacaoInline();
function fecharPainel() {
  painel.fechar();
  setErroPainel(null);
}
// no botão do PageHeader/Card:
<Button
  onClick={() => (painel.aberto ? fecharPainel() : painel.abrir())}
  aria-expanded={painel.aberto}
  aria-controls={painel.id}
>
  {painel.aberto ? 'Fechar' : 'Adicionar X'}
</Button>
// no componente:
<PainelCriacaoInline aberto={painel.aberto} id={painel.id} titulo="Novo X">...</PainelCriacaoInline>
```

Isso resolve os dois achados adiados da revisão final da Onda A (acessibilidade e boilerplate) sem
inventar um mecanismo que nenhuma tela real usaria. As 6 telas de Pessoas (Onda B) são o primeiro
consumidor real; `AtividadesTab.tsx` fica como está (não é migrada para o hook nesta rodada — troca de
`useState` por hook em código já aprovado, sem tela nova para justificar, entra na Onda F se fizer
sentido).

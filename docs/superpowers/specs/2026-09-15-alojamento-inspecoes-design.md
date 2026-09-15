# Alojamento em Inspeções — Design

Status: aprovado para virar plano de implementação.
Revisado por: usuário + codex-review (ver seção "Origem das decisões").

## 1. Contexto e objetivo

Hoje o módulo de Inspeções trata "Alojamento" como só mais um `TipoInspecao` genérico: o técnico
abre o painel "Nova inspeção", escolhe manualmente Checklist + Obra + Atividade + Data +
Responsável, e preenche um checklist de 30 itens (já seedado, 6 seções, ver
`ChecklistAlojamentoSeeder.cs`) através da tela genérica `InspecaoDetalhePage.tsx`.

Objetivo desta feature: dar ao técnico responsável um atalho dedicado — uma sub-aba "Alojamento"
dentro de Inspeções, com cards por obra e por alojamento (mesmo padrão visual de Pessoas >
Funcionários), onde clicar num alojamento já abre a inspeção pronta pra preencher, sem repetir
cadastro manual toda vez. Cada item ganha um seletor de 3 opções (bolinhas) e um slot de foto
obrigatório.

Não existe hoje, em lugar nenhum do sistema, um cadastro de "Alojamento" (nem local, nem vindo do
G-RH — confirmado lendo o contrato real da integração, que só traz dados de Colaborador). O G-RH
já tem esse cadastro do lado dele (telas reais vistas: alojamento com endereço + moradores
vinculados por colaborador), mas SST-APP não consome esse dado ainda.

## 2. Escopo desta primeira entrega

**Dentro do escopo:**
- Cadastro local de Alojamento no SST-APP (manual, via tela simples), pronto para receber
  sincronização do G-RH no futuro sem precisar de retrabalho.
- Nova sub-aba "Alojamento" em Inspeções, com cards de obra → cards de alojamento.
- Endpoint atômico de "criar ou retomar" inspeção de alojamento.
- Seletor de 3 bolinhas (Conforme/Não conforme/Não aplicável) **só na inspeção de Alojamento**
  (não é rollout global no `InspecaoDetalhePage`).
- `ExigeFotografia = true` nos 30 itens do checklist de Alojamento (mudança de dado real, não só
  visual).
- Configuração global de "dias para considerar inspeção atrasada".

**Fora do escopo (próximos passos, não bloqueiam esta entrega):**
- Sincronização automática com G-RH (fila `alojamento-grh` + endpoint de carga inicial) — pedido já
  foi redigido em `docs/superpowers/2026-09-15-pedido-integracao-alojamento-grh.md`, depende do
  time de lá.
- Levar o seletor de 3 bolinhas para os demais tipos de inspeção.
- Storage externo/blob para fotos (hoje `byte[]` na entidade, como já é o padrão do resto do
  sistema — foto obrigatória em todo item aumenta volume, mas não é motivo para mudar o padrão de
  armazenamento nesta entrega; é um risco a observar, não um bloqueio).

## 3. Modelo de dados

### 3.1 `Alojamento` (nova entidade)

| Campo | Tipo | Observação |
|---|---|---|
| `Id` | Guid | |
| `ObraId` | Guid (FK Obra) | |
| `Nome` | string | ex.: "ALOJAMENTO - 02" |
| `Endereco` | string? | |
| `GrhAlojamentoId` | string? | chave de sincronização futura, único quando não nulo |
| `OrigemCadastro` | enum (`Manual`, `Grh`) | default `Manual` |
| `DataUltimaSincronizacao` | DateTime? | preenchido só quando `OrigemCadastro = Grh` |
| `Ativo` | bool | inativação, nunca hard delete (ver 3.4) |

Removido do escopo: `CustoMensal`. É dado financeiro, não serve ao propósito de inspeção de SST, e
adicionaria uma superfície de dado sensível sem necessidade nesta entrega.

### 3.2 `AlojamentoMorador` (nova entidade)

| Campo | Tipo | Observação |
|---|---|---|
| `Id` | Guid | |
| `AlojamentoId` | Guid (FK) | |
| `TrabalhadorId` | Guid (FK Trabalhador) | reaproveita o Trabalhador já sincronizado do G-RH |
| `DataDesde` | DateTime | |
| `DataSaida` | DateTime? | null = morador atual |

**Invariante**: um `TrabalhadorId` só pode ter um vínculo ativo (`DataSaida IS NULL`) por vez —
índice único filtrado no banco, não só validação de aplicação.

**Exibição**: lista nominal de moradores aparece normalmente na tela (decisão do usuário — não
restringir a só contadores).

### 3.3 `Inspecao` — campo novo

- `AlojamentoId` (Guid?, FK) — obrigatório quando `TipoInspecao == Alojamento` (validado no
  domínio/comando, não só no front).
- Validação adicional: `Inspecao.ObraId` deve ser igual a `Alojamento.ObraId` do alojamento
  referenciado — evita inconsistência de RBAC entre obra da inspeção e obra do alojamento.
- A inspeção **congela** a versão do checklist no momento da criação (`ChecklistModeloId` já
  funciona assim hoje, por versionamento — só reforçar que a criação automática usa a versão
  vigente no momento do clique, e isso não muda depois se o catálogo for atualizado).
- Considerar guardar um snapshot leve (`AlojamentoNomeSnapshot`, `AlojamentoEnderecoSnapshot`) na
  inspeção para preservar o relatório histórico caso o cadastro do alojamento mude depois. (Nice-to-have; incluir se o custo for baixo, não é bloqueante.)

### 3.4 Regras de ciclo de vida

- Não permitir excluir um `Alojamento` com inspeções vinculadas — só inativar (`Ativo = false`).
- `AlojamentoMorador` "moradores atuais" = `DataSaida IS NULL` (e, se fizer sentido depois,
  `DataDesde <= hoje`).

### 3.5 Configuração de prazo ("atrasada")

Nova configuração global (não por obra, não por alojamento — um valor único para o sistema todo),
editável sem precisar de deploy:

- `ConfiguracaoAlojamento.DiasParaInspecaoAtrasada` (int, default sugerido: 30).
- Guardado em tabela própria (uma linha, mesmo padrão simples de outras configurações do sistema),
  editável por uma tela pequena em Administração.
- O cálculo de status do card ("nunca inspecionado" / "em dia" / "atrasada") usa esse valor —
  nunca um número fixo no front-end.

## 4. Fluxo: criar ou retomar inspeção (endpoint atômico)

Descartada a ideia original ("front busca em andamento; se não achar, cria") — tem race condition
real (dois técnicos clicando ao mesmo tempo no mesmo alojamento criariam duas inspeções).

**Desenho adotado:**

```
POST /api/alojamentos/{alojamentoId}/inspecao-atual
```

Dentro de uma transação:
1. Valida RBAC do usuário pela `ObraId` do alojamento.
2. Procura inspeção com `AlojamentoId` = este e `TipoInspecao = Alojamento` que ainda esteja em
   andamento (não concluída).
3. Se existir, retorna ela (o front navega pra ela, mostrando "Inspeção em andamento, criada por
   X em DD/MM/AAAA HH:mm" — feedback visível de que não é uma inspeção nova).
4. Se não existir, cria uma nova (obra + alojamento + checklist vigente + data de hoje +
   responsável = usuário atual) e retorna ela.
5. Índice único no banco (`AlojamentoId` + `TipoInspecao` + condição "em andamento") garante que
   nunca existam duas inspeções abertas para o mesmo alojamento, mesmo sob concorrência — a
   garantia fica no banco, não só na lógica da aplicação.

Auditoria: a criação automática registra responsável e timestamp normalmente (os mesmos campos que
toda `Inspecao` já tem hoje).

Concorrência na edição (depois de aberta): duas pessoas editando a mesma inspeção — usar
`UpdatedAt`/row version simples para não sobrescrever silenciosamente uma resposta de item pela
outra. Não é preciso edição colaborativa em tempo real nesta entrega, só evitar perda silenciosa de
dado.

## 5. Navegação e UI

### 5.1 Nova sub-aba

`InspecoesPage.tsx`: adicionar `'alojamento'` ao array `ABAS`, rótulo "Alojamento", renderizar novo
componente `AlojamentoTab.tsx`.

### 5.2 Cards de obra (grade nível 1)

Mesmo padrão visual de `TrabalhadoresTab.tsx` (grade de `Card`, agregação client-side). RBAC:
mesmo filtro de obras que já existe em todo o sistema (o usuário só vê as obras que já tem acesso
hoje — nada novo a construir em autorização aqui).

Cada card mostra: nome/código da obra, "N alojamento(s)", "M morador(es)", e um chip de status
geral (ex.: "todos em dia" / "N pendente(s)" / "sem alojamento cadastrado").

Obras sem nenhum alojamento cadastrado precisam de um caminho pra cadastrar um ali mesmo (botão
"Cadastrar alojamento") — senão a sub-aba vira beco sem saída pra várias obras.

### 5.3 Cards de alojamento (grade nível 2, ao clicar numa obra)

Nome, endereço, contagem de moradores (com nomes visíveis, ver 3.2), e chip de status da última
inspeção calculado com a configuração de 5.5 (nunca inspecionado / em dia / atrasada).

### 5.4 Tela de inspeção

Reaproveita `InspecaoDetalhePage.tsx` (tela genérica, sem duplicar rota). A diferença visual
(seletor de 3 bolinhas em vez do `<Select>` atual) é escopada **só para inspeções de Alojamento** —
implementar como componente novo e reutilizável, mas com rollout condicional
(`tipoInspecao === Alojamento` ou prop equivalente), não substituir o `<Select>` globalmente nesta
entrega.

Cada item mantém o `SlotFoto` (componente já existente, reaproveitado sem mudança) — a diferença é
que agora é obrigatório (ver seção 6), não só "sempre visível".

## 6. Foto obrigatória por item

`ExigeFotografia` vira `true` nos 30 itens do `ChecklistModeloItem` do checklist de Alojamento —
isso é mudança de regra de negócio, não só de renderização:

- Atualizar `ChecklistAlojamentoSeeder.cs` para novos ambientes.
- Se o checklist já foi seedado em bases reais (hml/produção), criar uma migração/comando de
  atualização dos itens existentes, em vez de confiar só no seeder (que só roda se nenhum
  `ChecklistModelo` de Alojamento existir ainda).
- Validar na conclusão da inspeção: item com `ExigeFotografia = true` sem foto bloqueia a conclusão
  (mensagem clara indicando quais itens faltam).

## 7. Integração futura com G-RH

Pedido já foi redigido e enviado (ver
`docs/superpowers/2026-09-15-pedido-integracao-alojamento-grh.md`). Preparar o domínio agora para
não retrabalhar depois:

- Upsert idempotente por `GrhAlojamentoId` (nunca por nome/endereço).
- `OrigemCadastro` decide estratégia de conflito: se o usuário editou manualmente um alojamento
  `Manual` e depois o G-RH manda dado equivalente, decidir explicitamente quem vence (sugestão:
  dado do G-RH vence campos de identificação/endereço; nunca sobrescreve inativação manual sem
  reativar).
- Histórico de moradores baseado em eventos de entrada/saída (o próprio `AlojamentoMorador` já
  serve para isso, com `DataDesde`/`DataSaida`).
- Pedir também um endpoint de carga inicial, não só a fila — fila sozinha sem reconciliação
  costuma dar retrabalho em caso de perda de mensagem, replay, ambiente novo, ou correção
  histórica.

## 8. Riscos e decisões conscientes

- **RBAC**: sempre validado no backend, nunca só filtro de grade no front (a grade de obras já é
  RBAC-filtrada hoje pelo mesmo mecanismo usado em Trabalhadores/Obras — só reforçar que a rota de
  alojamento/inspeção-atual também valida).
- **Fotos em volume**: 30 fotos obrigatórias por inspeção, armazenadas como `byte[]` na entidade
  (padrão atual do sistema) — aceito para esta entrega, mas é um ponto a observar se o volume de
  inspeções de alojamento crescer muito (métricas de tamanho de banco depois do primeiro mês em
  produção).
- **UX de criação automática**: o clique no card tem efeito colateral (cria uma inspeção). Precisa
  de feedback claro na tela (ver seção 4, item 3) para não parecer uma ação "muda".
- **Dados sensíveis**: moradores exibidos nominalmente por decisão do usuário — não há restrição
  de permissão adicional nesta entrega além do RBAC de obra já existente.

## 9. Origem das decisões

Design inicial: brainstorming direto com o usuário, a partir de dois screenshots (tela de
Inspeções atual do SST-APP + tela de Alojamentos do G-RH) e de um protótipo HTML navegável validado
antes deste documento.

Revisão técnica: `codex exec` (Codex CLI) revisou o desenho contra o código real do repositório e
sugeriu as correções incorporadas nas seções 3-8 (endpoint atômico em vez de check-then-create,
escopo do seletor de 3 bolinhas restrito a Alojamento, `ExigeFotografia` como regra real, e os
riscos listados na seção 8). Duas decisões do usuário sobrepõem sugestões do Codex conscientemente:
moradores nominais ficam visíveis (Codex sugeriu só contadores), e o prazo de atraso é uma
configuração única global, não por obra (Codex não tinha proposto escopo específico para isso).

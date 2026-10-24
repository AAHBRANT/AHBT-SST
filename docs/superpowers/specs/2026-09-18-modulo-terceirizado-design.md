# Módulo Terceirizado — Design

**Data:** 2026-09-18
**Status:** Aprovado para planejamento de implementação

## 1. Objetivo

Criar um módulo "Terceirizado" para gerenciar empresas terceirizadas e as
pessoas que elas alocam nas obras, cobrindo:

- Cadastro de empresa terceirizada.
- Contratos recebidos automaticamente do G-Juri (sistema jurídico, em
  desenvolvimento) quando validados.
- Vínculo de pessoas terceirizadas ao `Trabalhador` já existente.
- Derivação automática de EPI obrigatório (com checagem de estoque) e
  treinamentos obrigatórios por função, reaproveitando estruturas já
  maduras no sistema (`MatrizEpiFuncao`, `EstoqueEpi`,
  `MatrizTreinamentoFuncao`).
- Bloqueio de liberação da pessoa até que todas as pendências de
  segurança estejam resolvidas.

## 2. Contexto atual (levantado antes do desenho)

- Arquitetura do SST: Clean Architecture + CQRS/MediatR (Domain /
  Application / Infrastructure / Api), frontend React em
  `AAHBRANT.SST.TeamsApp`.
- Não existe hoje cadastro estruturado de empresa terceirizada — havia
  uma entidade `EmpresaUnidade`, removida (migration
  `20260820202034_RemoverEmpresaUnidade`). `Trabalhador.Vinculo` já tem
  o valor `Terceirizado`, mas sem FK para empresa.
- `Aso` já existe como entidade completa (CRUD + sincronização com
  G-RH), ligada a `Trabalhador` via `TrabalhadorId`. Será reaproveitada
  sem alterações estruturais.
- `MatrizEpiFuncao` (Função → EPI obrigatório), `EstoqueEpi` +
  `MovimentacaoEstoqueEpi` (estoque por Obra) e
  `MatrizTreinamentoFuncao` (Função → treinamento obrigatório) já
  existem e estão maduros — serão consumidos, não recriados.
- G-Juri hoje (branch `feature/g-juri-api-busca-processos`, ainda não
  mergeada) só busca processos judiciais (DJEN/DataJud). Não existe
  nenhum conceito de "Contrato" de prestação de serviço em nenhum
  sistema hoje — será modelado do zero, com o G-Juri assumindo esse
  papel no futuro (hoje só desenhado no papel, nada implementado).
- Convenção atual do frontend: módulos novos entram como aba dentro de
  uma das 4 páginas-pilar (`Gestão de SST`, `Operação`, `Pessoas`,
  `Ocorrências`), evitando itens novos na sidebar. **Decisão explícita
  do usuário: quebrar essa convenção** — "Terceirizado" será um item
  próprio na sidebar, dado o peso do módulo.
- Existe Motor de Alertas (sino/Activity Feed do Teams) já em produção,
  usado para notificações — será reaproveitado para todos os alertas
  deste módulo.

## 3. Escopo

### Dentro do escopo (v1, desenhada de uma vez conforme pedido do usuário)

- Cadastro de Empresa terceirizada.
- Recebimento de Contrato via webhook do G-Juri (empresa + obra +
  vagas por função).
- Cadastro de pessoa terceirizada preenchendo uma vaga do contrato
  (reaproveitando `Trabalhador`).
- Derivação automática de EPI obrigatório por função + checagem/reserva
  de estoque por obra + tarefa de entrega.
- Derivação automática de treinamentos obrigatórios por função +
  regra fixa de "Integração de Segurança" obrigatória para todo
  terceirizado.
- Bloqueio de liberação da pessoa até resolver todas as pendências de
  segurança.
- Alertas via Motor de Alertas para: falta de estoque de EPI, contrato
  encerrado com pessoas ainda ativas.
- Telas: Empresas, Contratos, Pessoas (view filtrada de Trabalhador),
  Pendências/Alertas.

### Fora do escopo (explicitamente adiado)

- Documentos obrigatórios da própria empresa terceirizada com controle
  de vencimento (CND, seguro, PGR da terceirizada) — v1 permite anexar
  documento genérico (via `Evidencia`), sem exigência nem alerta de
  vencimento.
- Desenho do fluxo interno de elaboração/validação de contrato dentro
  do G-Juri (é responsabilidade de outro projeto/sistema). Este
  documento define apenas o contrato de integração (payload do
  webhook) do ponto de vista do SST.
- Desligamento automático de pessoas quando o contrato é encerrado
  (v1 só alerta).
- EPI/Uniforme com regras diferentes das já existentes para
  trabalhador CLT — terceirizado usa exatamente a mesma matriz e
  estoque, sem regras especiais.
- Contrato cobrindo múltiplas obras (v1: 1 obra por contrato).
- Contrato com pessoas já nomeadas vindas do G-Juri (v1: só quantidade
  por função; nomes são cadastrados depois no SST).

## 4. Modelo de dados

### `Empresa` (nova entidade, `AuditableEntity`)

| Campo | Tipo | Observação |
|---|---|---|
| RazaoSocial | string | obrigatório |
| NomeFantasia | string? | |
| Cnpj | string | obrigatório, único |
| TipoServicoPrestado | string | texto livre |
| ContatoNome / ContatoTelefone / ContatoEmail | string? | responsável da empresa |
| Status | enum `StatusEmpresa` (Ativa, Inativa) | |
| Evidencias | coleção `Evidencia` (`EntidadeTipo = Empresa`) | documentos livres, sem obrigatoriedade/vencimento na v1 |

### `Contrato` (nova entidade)

| Campo | Tipo | Observação |
|---|---|---|
| EmpresaId | FK Empresa | |
| ObraId | FK Obra | 1 obra por contrato |
| NumeroContrato | string | |
| DataInicioVigencia / DataFimVigencia | DateOnly | |
| Status | enum `StatusContrato` (Validado, Encerrado, Cancelado) | |
| GJuriContratoId | string | identificador de origem, único — garante idempotência do webhook |

### `ContratoVagaFuncao` (nova entidade)

| Campo | Tipo | Observação |
|---|---|---|
| ContratoId | FK Contrato | |
| FuncaoId | FK Funcao | |
| QuantidadeVagas | int | vindo do G-Juri |
| QuantidadePreenchidas | int | calculado a cada pessoa cadastrada nessa vaga |

### `Trabalhador` (extensão da entidade existente)

- `EmpresaId` (FK Empresa, nullable) — preenchido quando `Vinculo = Terceirizado`.
- `ContratoId` (FK Contrato, nullable) — idem.
- Cadastrar uma pessoa com `EmpresaId`/`ContratoId`/`FuncaoId` incrementa
  `ContratoVagaFuncao.QuantidadePreenchidas` da vaga correspondente.
- Novo campo de status derivado (não persistido, calculado): `Pendente`
  ou `Liberada` — ver seção 7.

## 5. Integração com G-Juri (webhook)

- **Direção**: G-Juri chama o SST (o SST não faz polling nem lê banco
  direto — diferente do padrão usado com o G-RH, pois o G-Juri está
  sendo construído agora e pode expor uma API própria desde já).
- **Endpoint**: `POST /api/integracoes/gjuri/contratos/validados` e
  `.../contratos/encerrados`. **Atualização pós-brainstorming (definida
  durante o planejamento, após levantamento do código):** autenticação
  via Entra ID App Role client-credentials — mesmo mecanismo já usado
  pela integração G-RH existente (`AppRolesReconhecidas.cs`) — em vez
  do `X-Api-Key` originalmente esboçado aqui. Justificativa: o SST já
  tem esse mecanismo maduro para integrações inbound (rotação e
  auditoria via Entra ID, sem segredo estático trafegando), e o
  `X-Api-Key` é só como a própria API do G-Juri protege quem a
  consome — não o padrão de entrada do SST. A App Role dedicada
  (`Sst.ReceberContratosGJuri`) é mapeada para uma permissão própria
  do webhook (`terceirizado:integracao-gjuri`), não reaproveitando
  `terceirizado:criar`, para que a permissão de um usuário humano
  cadastrar empresas nunca autorize, mesmo que indiretamente, quem
  pode chamar o webhook.
- **Eventos**:
  - `ContratoValidado`: payload com dados da empresa (CNPJ, razão
    social, nome fantasia, contato), dados do contrato (número,
    vigência, `ObraId`, `GJuriContratoId`) e lista de
    `{ FuncaoId ou nome da função, Quantidade }`.
  - `ContratoEncerrado`: payload com `GJuriContratoId` e data de
    encerramento.
- **Processamento (upsert idempotente por `GJuriContratoId`)**:
  - Empresa: se já existe pelo CNPJ, reaproveita; senão cria.
  - Contrato: cria se não existe (idempotência pelo `GJuriContratoId`);
    se o evento repetir, não duplica.
  - Vagas: cria/atualiza `ContratoVagaFuncao` por função informada.
  - `ContratoEncerrado`: marca `Status = Encerrado` e dispara alerta
    (seção 8) — não desliga ninguém automaticamente.
- **Resolução de `FuncaoId`**: **atualização pós-brainstorming.** Em vez
  de o G-Juri conhecer o `Guid` interno do `Funcao.Id` do SST (o que
  acoplaria os dois sistemas por ambiente — dev/homologação/produção
  teriam IDs diferentes), o payload traz o **nome exato da função**
  (`FuncaoNome`, string), e o SST resolve para o `Funcao.Id`
  correspondente por nome. Função não encontrada por nome é erro
  (`KeyNotFoundException`), rejeitando o evento — mais simples e
  estável entre ambientes do que sincronizar identificadores internos.

## 6. Automação de EPI

Disparada quando uma pessoa é cadastrada numa vaga (`Trabalhador` criado
com `EmpresaId`/`ContratoId`/`FuncaoId` preenchidos):

1. Consulta `MatrizEpiFuncao` da `FuncaoId` da pessoa → lista de EPIs
   obrigatórios.
2. Para cada EPI, consulta `EstoqueEpi` da `ObraId` do contrato:
   - **Saldo suficiente**: registra `MovimentacaoEstoqueEpi` do tipo
     Reserva (baixa o saldo disponível) e cria uma tarefa de entrega
     pendente, reaproveitando o fluxo de entrega de EPI já existente
     (pré-preenchido com trabalhador + EPI + quantidade).
   - **Saldo insuficiente**: gera alerta via Motor de Alertas para
     todos os técnicos de segurança vinculados à `ObraId`, informando
     EPI e quantidade faltante. A tarefa de entrega fica pendente até
     entrada de estoque.
3. Entrega é confirmada manualmente por uma pessoa (não há baixa
   automática de "entregue") — reaproveita o fluxo de confirmação de
   entrega de EPI já existente.

## 7. Automação de Treinamento

Disparada no mesmo momento do cadastro da pessoa:

1. Consulta `MatrizTreinamentoFuncao` da `FuncaoId` → lista de cursos
   obrigatórios para a função (reaproveita `ListarTreinamentosObrigatoriosPorFuncaoQuery`
   já existente).
2. **Regra nova e fixa do módulo Terceirizado** (não depende da
   matriz): toda pessoa com `Vinculo = Terceirizado` também precisa de
   "Integração de Segurança" antes de ser liberada — verificação
   própria do módulo, não um registro na `MatrizTreinamentoFuncao`.
3. Pendências de treinamento aparecem no painel de Pendências/Alertas
   e na ficha da pessoa; matrícula/participação em turma continua
   usando o módulo de Treinamentos já existente.

## 8. Regras de bloqueio e alertas

**Status da pessoa terceirizada** (calculado, exibido na listagem e na
ficha):

- **Liberada**: ASO válido **e** Integração de Segurança concluída
  **e** todos os treinamentos obrigatórios da matriz da função
  concluídos e válidos **e** todos os EPIs obrigatórios da função
  confirmados como entregues.
- **Pendente**: qualquer uma das condições acima não satisfeita — a
  pessoa não pode ser marcada como ativa/liberada para trabalhar
  enquanto isso não for resolvido.

**Alertas via Motor de Alertas** (sino/Activity Feed do Teams),
direcionados aos técnicos de segurança vinculados à Obra:

- Falta de estoque de EPI (seção 6).
- Contrato encerrado com pessoas ainda ativas vinculadas (sem
  desligamento automático).

Pendências de ASO/treinamento/EPI aparecem no painel de
Pendências/Alertas e na ficha da pessoa, sem necessidade de alerta
"empurrado" adicional (o bloqueio de status já é o sinal principal).

## 9. Telas

- **Empresas**: lista (busca por CNPJ/razão social), cadastro/edição,
  ficha (dados cadastrais, contratos, pessoas vinculadas, documentos
  anexos via `Evidencia`).
- **Contratos**: lista com status/vigência/vagas preenchidas × total,
  acessível a partir da ficha da empresa. Cada função com vaga aberta
  tem ação "Cadastrar pessoa", que abre o cadastro de `Trabalhador`
  pré-preenchido com Empresa/Contrato/Função (função fica bloqueada
  para edição, vinda da vaga).
- **Pessoas**: view de `Trabalhador` filtrada por
  `Vinculo = Terceirizado`, com coluna de status (Pendente/Liberada) e
  indicadores de qual pendência falta (ASO, integração, treinamento,
  EPI). Reaproveita a ficha de Trabalhador já existente, com aba extra
  mostrando Empresa/Contrato/Função e detalhamento de pendências.
- **Pendências/Alertas**: painel consolidado — pessoas bloqueadas e o
  motivo, contratos encerrados com gente ainda ativa, faltas de
  estoque de EPI.
- **Navegação**: novo item próprio na sidebar "Terceirizado" (decisão
  explícita do usuário, quebrando a convenção atual de módulos como
  aba de pilar existente).

## 10. Pontos técnicos resolvidos durante o planejamento

- Resolução de `FuncaoId`: por nome (`FuncaoNome`), não por Guid — ver
  seção 5.
- Autenticação do webhook: Entra ID App Role, não `X-Api-Key` — ver
  seção 5.
- "Técnicos de segurança vinculados à Obra": não existia consulta
  pronta — foi criada como serviço próprio
  (`ITecnicosSegurancaPorObraService`) no plano de implementação.

## 11. Riscos aceitos conscientemente (débito técnico transversal, não deste módulo)

Levantados numa segunda revisão técnica independente durante o
planejamento (Codex CLI) e mantidos deliberadamente fora do escopo
desta v1 porque são características do sistema como um todo, não algo
que este módulo introduz:

- **Concorrência em estoque/vagas**: o mesmo padrão de "ler saldo,
  validar, decrementar, salvar" sem transação explícita ou tratamento
  de `DbUpdateConcurrencyException` já existe hoje em
  `CriarEntregaEpiCommandHandler` (módulo EPI manual, entregas). O
  módulo Terceirizado reaproveita o mesmo padrão para manter
  consistência — corrigir isso isoladamente aqui criaria duas
  semânticas de estoque diferentes dentro do mesmo agregado
  `EstoqueEpi`. Uma correção de concorrência, se necessária, deve ser
  uma melhoria transversal do módulo EPI/Estoque como um todo.
- **Escopo RBAC por Obra**: `PermissaoAuthorizationHandler.cs` já
  documenta que a Camada 2 (autorização por obra) e a Camada 3
  (Global Query Filter) estão **deliberadamente pendentes** em todo o
  sistema — nenhum módulo hoje filtra automaticamente pelo escopo de
  obra do usuário. Os endpoints de Empresas/Contratos/Pessoas/
  Pendências deste módulo seguem a mesma política de permissão (sem
  escopo por obra) dos demais controllers existentes
  (`TrabalhadoresController`, `EntregasEpiController` etc.) — quando
  esse débito for endereçado globalmente, este módulo deve aderir ao
  mesmo mecanismo.
- **Cancelamento/reversão de EPI reservado**: se uma pessoa é
  cadastrada numa vaga e um EPI é reservado (estoque decrementado,
  `EntregaEpi.Confirmada = false`), a v1 não tem um fluxo de
  cancelamento caso a pessoa nunca seja efetivamente liberada (ex.:
  desistência antes da confirmação física). Aceito como simplificação
  de v1 — o estorno, se necessário, é manual via ajuste de estoque já
  existente (`AjustarEstoqueEpiCommand`).

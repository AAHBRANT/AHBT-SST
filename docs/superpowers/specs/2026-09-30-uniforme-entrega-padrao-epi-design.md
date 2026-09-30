# Entrega de Uniforme no padrão do EPI — Design

Data: 2026-09-30 · Status: aprovado pelo usuário

## Objetivo

Levar a entrega de Uniforme ao mesmo esquema da entrega de EPI: carrinho com várias peças,
uma única assinatura do funcionário, devolução com estorno, ficha consolidada em PDF e aba
própria no perfil do funcionário.

## Fora de escopo (diferença legítima com o EPI)

CA, validade de CA, trava NR-06, `DataValidade`/vida útil, alertas de vencimento, automação do
Terceirizado, evidências fotográficas. Motivo "Desgaste" continua; não se cria "Vencimento".

## 1. Carrinho + assinatura única

- `EntregaUniformeTab.tsx` passa de `Card` fixo para `PainelCriacaoInline` com 2 seções:
  "Quem recebe" (`SeletorPesquisavel`) e "O que é entregue".
- "O que é entregue": lista todas as peças da matriz da função do funcionário, cada linha com
  foto, tamanho resolvido do cadastro (não editável), saldo de estoque da obra naquele tamanho e
  quantidade. Peça sem tamanho cadastrado aparece bloqueada com atalho para o perfil. Motivo,
  data e observações valem para o carrinho inteiro.
- Confirmar: cria uma `EntregaUniforme` por peça via `POST` existente, em sequência (mesma
  semântica do EPI: falha parcial mantém o que já gravou e informa).
- Assinatura: o diálogo de lote do EPI (`AssinaturaEntregaEpiLoteDialog`) é generalizado para
  receber o tipo de entidade (`EntregaEpi` | `EntregaUniforme`) e o texto do termo. Sem mudança
  de banco: um `DocumentoAssinatura` por entrega, a mesma interação física (digital ou facial)
  é propagada a todos. O EPI mantém comportamento idêntico.
- Cada linha da lista ganha "Assinar" para reabrir assinatura pendente.

## 2. Devolução

- Domínio: `EntregaUniforme` ganha `DataDevolucao` (DateTime?, igual ao EPI) e `QuantidadeDevolucao` (int?).
  Migration só aditiva.
- `TipoMovimentacaoEstoqueUniforme` ganha `DevolucaoEntrada = 3` (valor livre; 4 já é IntegracaoGsupri).
- `PUT /api/EntregasUniforme/{id}` (`AtualizarEntregaUniformeCommand`): registra devolução,
  valida `0 < QuantidadeDevolucao <= Quantidade` e data >= data da entrega, e estorna ao estoque
  do mesmo catálogo+tamanho+obra a diferença em relação à devolução anterior (idempotente).
- `ExcluirEntregaUniformeCommand` passa a estornar só o que ainda está com o funcionário.
- Motor de Assinatura: novo rótulo `DevolucaoUniforme`. Frontend: devolução inline na linha +
  `AssinaturaDevolucaoUniformeDialog` (espelho do de EPI).

## 3. Ficha de Uniforme em PDF

- `GET /api/EntregasUniforme/ficha-trabalhador/{trabalhadorId}/pdf`
  (`ExportarFichaUniformeTrabalhadorQuery` + `FichaUniformePdfService`), no padrão da ficha de
  EPI: cabeçalho padrão, dados do funcionário, termo (4 cláusulas, sem CA/NR-6), tabela de
  entregas e devoluções com as assinaturas, carimbo de rastreabilidade `FichaUniformeTrabalhador`.
- Rótulo `FichaUniformeTrabalhador` no Motor de Assinatura; página pública agrega assinaturas
  das entregas como a ficha de EPI.
- ⚠️ Texto do termo precisa de validação do jurídico/QSMS antes de produção.

## 4. Perfil do funcionário

- Nova aba "Uniforme" em `TrabalhadorDetalhePage.tsx`: tamanhos (movidos da aba Geral,
  autorizado pelo usuário), histórico de entregas/devoluções e botão da ficha PDF.
- `GET /api/EntregasUniforme?trabalhadorId=` já existe; passa a expor `DataDevolucao`,
  `QuantidadeDevolucao` e `ObraId`.

## 5. RBAC

- `EstoqueUniforme` (e movimentações) recebem o mesmo filtro de acesso por obra que o
  `EstoqueEpi` tem em `SstDbContext`. Atenção à regra conhecida: filtro RBAC em JOIN de rota
  anônima quebra só em produção — nenhuma rota anônima deve depender disso.

## Testes

- Application.Tests: devolução com estorno (total, parcial, repetida), devolução acima do
  entregue, data inválida, exclusão após devolução parcial, query da ficha.
- Verificação visual local: carrinho com 3 peças → 1 assinatura → devolução → PDF → aba no
  perfil. Sem deploy até validação do usuário.

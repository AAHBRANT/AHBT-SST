# Módulo Uniforme — Design

**Data:** 2026-09-07
**Status:** Aprovado pelo usuário em brainstorming (2026-09-07), pronto para plano de implementação.

## Contexto e objetivo

O sistema já controla entrega e estoque de EPI (Equipamento de Proteção Individual), travado pela
matriz de EPI por função do trabalhador (`MatrizEpiFuncao`, `EntregaEpi`, `CatalogoEpi`). O usuário
pediu um módulo novo, **Uniforme**, para controlar a mesma coisa só que para peças de uniforme
(camisa, calça, bota etc.) — "na pegada de EPI", ou seja, reaproveitando o mesmo padrão de
catálogo + matriz por função + entrega + estoque, sem escolha manual de item na entrega (travado
pela matriz da função do trabalhador, sem válvula de escape — mesmo princípio já usado no EPI).

A diferença central em relação ao EPI é que uniforme tem uma dimensão de **tamanho** por peça
(camisa em P/M/G/GG, calça/bota por numeração), que o EPI não trata como conceito de primeira
classe (hoje resolve isso duplicando o item no catálogo, ex.: "Bota nº 40", "Bota nº 42").

## Onde aparece na navegação

Nova aba **"Uniforme"** dentro do pilar **Operação** (`OperacaoPage`), ao lado da aba "EPI/EPC" —
não vira item de 1º nível na sidebar. Segue o padrão de sub-abas já usado no EPI: Entrega /
Estoque / Catálogo / Matriz por Função.

O cadastro de tamanho do trabalhador entra como uma nova seção na tela de perfil dele
(`TrabalhadorDetalhePage`), ao lado das demais seções de dados cadastrais (Treinamentos, EPI,
Cursos etc.).

## Modelo de dados

Nomenclatura espelha as entidades já existentes de EPI (`CatalogoEpi` → `CatalogoUniforme`,
`MatrizEpiFuncao` → `MatrizUniformeFuncao`, `EntregaEpi` → `EntregaUniforme`).

### `CatalogoUniforme`
A peça em si — sem tamanho embutido no nome/registro.
- `Id`
- `Nome` (ex.: "Camisa", "Calça", "Bota", "Colete")
- `Categoria` (opcional, texto livre)
- `Ativo` (bool)

### `EstoqueUniforme`
Grade de estoque por tamanho — uma peça pode ter N tamanhos, cada um com sua própria quantidade.
- `Id`
- `CatalogoUniformeId` (FK)
- `Tamanho` (texto livre — cada peça usa a convenção que fizer sentido: "P"/"M"/"G"/"GG" para
  camisa, numeração tipo "42" para calça/bota; **sem enum fixo**, pois a granularidade varia por
  peça)
- `Quantidade` (int, >= 0)
- Índice único em `(CatalogoUniformeId, Tamanho)` — um bucket por combinação peça+tamanho.

### `MatrizUniformeFuncao`
Autorização por função — aponta só para a peça, sem fixar tamanho (o tamanho vem do trabalhador
que está recebendo, resolvido em tempo de entrega).
- `Id`
- `FuncaoId` (FK)
- `CatalogoUniformeId` (FK)

### `TrabalhadorTamanhoUniforme`
O tamanho de cada trabalhador, por peça — tabela própria (não campos fixos tipo `TamanhoCamisa` no
cadastro do trabalhador), para que uma peça nova no catálogo não exija alterar o cadastro do
trabalhador de novo.
- `Id`
- `TrabalhadorId` (FK)
- `CatalogoUniformeId` (FK)
- `Tamanho` (texto livre, mesma convenção usada em `EstoqueUniforme.Tamanho` para aquela peça)
- Índice único em `(TrabalhadorId, CatalogoUniformeId)` — um tamanho por peça por trabalhador.

### `EntregaUniforme`
Registro de entrega — mesmo formato de `EntregaEpi` (trabalhador, item, quantidade, motivo, data),
mais o tamanho resolvido no momento da entrega (snapshot, não referência viva a
`TrabalhadorTamanhoUniforme` — se o trabalhador trocar de tamanho depois, o histórico de entregas
antigas não deve mudar retroativamente).
- `Id`
- `TrabalhadorId` (FK)
- `CatalogoUniformeId` (FK)
- `Tamanho` (snapshot, texto)
- `Quantidade` (int)
- `MotivoTipo` (mesmo enum/tipo já usado em `EntregaEpi`, ex.: entrega inicial, substituição por
  desgaste)
- `DataEntrega`

## Fluxo de Entrega

Reaproveita a tela "Entrega Rápida" do EPI como referência de UX:

1. Busca o trabalhador.
2. Sistema resolve automaticamente, para cada `CatalogoUniformeId` presente na matriz da função do
   trabalhador, o tamanho dele (via `TrabalhadorTamanhoUniforme`) — mostra a lista de peças já com
   tamanho resolvido (ex.: "Camisa (M)", "Bota (40)").
3. **Bloqueios, sem válvula de escape** (mesmo princípio do EPI: travar de verdade, não só avisar):
   - Matriz da função vazia → bloqueia com mensagem pedindo para cadastrar a matriz da função antes.
   - Trabalhador sem tamanho cadastrado para alguma peça exigida pela matriz → bloqueia essa peça
     especificamente, com mensagem pedindo para completar o cadastro de tamanho do trabalhador.
   - Estoque zerado no bucket (peça + tamanho) resolvido → bloqueia essa peça, com mensagem
     indicando que o estoque daquele tamanho está zerado.
4. Confirmação → assinatura biométrica, reaproveitando o mesmo diálogo de assinatura em lote já
   usado no EPI (`AssinaturaLoteEntregaEpiDialog`, generalizado ou duplicado para Uniforme).
5. Ao confirmar a entrega, o sistema **baixa a quantidade entregue do bucket** `EstoqueUniforme`
   correspondente (mesma peça + tamanho resolvido) — mesmo princípio de baixa de estoque já
   aplicado em `EntregaEpi`.

## Fluxo de Estoque

Cadastro manual — sem código de barras (uniformes normalmente não vêm com código de barras
individual de fábrica, diferente de muitos itens de EPI):

1. Escolhe a peça no catálogo.
2. Escolhe um tamanho já existente na grade daquela peça, ou cria um tamanho novo direto na tela
   (sem precisar de uma tela de administração separada).
3. Digita a quantidade recebida — soma na quantidade daquele bucket.

## Permissões

Reaproveita o RBAC por Obra já existente no sistema (mesmo escopo de Camada 2/3 já aplicado ao
EPI) — nenhuma regra de permissão nova.

## Fora de escopo (YAGNI, não mencionado pelo usuário)

- Pedido/solicitação de uniforme pelo próprio trabalhador (fluxo de aprovação) — não pedido.
- Código de barras no estoque de Uniforme — descartado explicitamente em favor de cadastro manual.
- Orçamento por obra / controle financeiro de compra de uniforme — não mencionado.
- Fotos de evidência do uniforme entregue — não mencionado (EPI moderno tem esse fluxo de
  confirmação por foto na Entrega Rápida; não foi pedido aqui, mas pode ser avaliado depois caso o
  usuário queira paridade total com o fluxo mais recente de EPI).
- **Manual de Uniforme** (sub-aba com um PDF por Função, ligado à Matriz) — discutido em
  brainstorming (2026-09-07), chegou a ser desenhado (entidade `ManualUniformeFuncao`, PDF por
  `FuncaoId`), mas o usuário decidiu deixar de fora por enquanto ("deixa sem manual por
  enquanto"). Referência mencionada pelo usuário para quando isso for retomado: arquivo antigo
  `Manual de Epi's.pptx` (pasta "02.Obsoleto" do SGI/SST no OneDrive do Junior Peixoto).
- **Acervo geral de documentos PDF** (aba própria, ex.: placas de sinalização, outros documentos de
  referência) — ideia do usuário para um módulo futuro, maior que Uniforme e não ligado
  especificamente a ele. Explicitamente adiado ("fica pra depois desse"), não faz parte deste
  design.

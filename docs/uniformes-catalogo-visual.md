# Uniformes: catálogo visual e preservação dos registros

Entrega autorizada em 30/10/2026, a partir do HTML aprovado pelo usuário.

## Dados existentes

- A migration `CatalogoVisualUniforme` adiciona somente `CatalogoUniformes.ReferenciaVisual`.
- Padroniza o nome no próprio cadastro (maiúsculas e correção BROM → BRIM).
- O usuário confirmou que os dois itens legados, CALÇA BRIM e CAMISA BRIM MANGA LONGA, são laranja. Os nomes genéricos exatos são associados às fichas laranja.
- Não cria nem remove catálogo, estoque, trabalhador, tamanho, entrega, movimentação ou assinatura. Não distribui saldo entre cores. A foto original continua armazenada para retorno ao modelo anterior; a referência passa a fornecer a ficha aprovada no endpoint da foto.
- As outras oito fichas podem ser escolhidas ao criar/editar o catálogo. Não geram estoque nem autorização automática por função. A matriz existente continua sendo a fonte de autorização.
- Na edição do catálogo, selecionar “Foto própria / sem modelo visual” remove a associação à ficha e permite usar fotos próprias. Um novo upload também remove essa associação.
- O rollback de schema remove a coluna de referência, mas mantém os nomes padronizados. Não se tenta adivinhar a grafia anterior.

## Entrega

Cards mostram foto, cor, funções efetivamente atribuídas, indicação da ficha, tamanho selecionável e saldo por tamanho na obra do trabalhador. Os tamanhos do perfil apenas sugerem a seleção; não são modificados pela entrega. O tamanho escolhido é guardado no histórico da entrega.

O carrinho usa `POST /api/entregasuniforme/lote`, que valida todo o kit antes de uma única SaveChanges transacional. RowVersion do estoque protege contra baixas concorrentes. IDs derivados da operação tornam o reenvio idempotente mesmo se a resposta se perder antes da gravação do cache do middleware. Não permite peças duplicadas no mesmo kit. O endpoint antigo continua compatível, usando o tamanho do perfil quando o cliente não informa um tamanho.

Após a confirmação, usa `AssinaturaEntregaUniformeLoteDialog` existente, com facial ou digital. Não acrescenta assinatura desenhada nem altera documentos assinados. O registro e a assinatura continuam sendo etapas distintas, como no fluxo anterior.

## Validação

- Testes Application de uniforme: preservação de ID, saldo, tamanhos, histórico e foto original; fichas incorporadas; lote inteiro; falta de saldo sem gravação parcial; reenvio sem nova baixa; funções reais da matriz.
- Playwright `playwright.uniforme.config.ts`: histórico legado visível, consulta por tamanho, limite de quantidade, kit com tamanhos escolhidos, chave de reenvio estável, abertura do diálogo existente de assinatura e bloqueio em falha de consulta do saldo. APIs simuladas; não valida leitor físico ou reconhecimento facial real.
- Build/PWA e verificação de coerência do modelo com a migration antes de publicar.

Após deploy, conferir os dois IDs legados e os saldos na aba Estoque. Não usar valores do protótipo como saldo inicial. Nenhuma imagem de uniforme representa evidência de captura em campo.

# G-SUPRI → SST: contrato de recebimentos v1

Solicitação do usuário em 29/09/2026: preparar o SST para a integração com o G-SUPRI,
mantido por outra equipe, eliminando a redigitação das entradas de estoque.
Este documento define o **contrato oferecido pelo SST**. Não afirma que o G-SUPRI já
possui API, webhook ou todos estes campos. A outra equipe deve confirmar/adaptar o envio.
As entidades, situações e regras aqui descritas são decisões não literais da Base de
Conhecimento, motivadas por essa solicitação e pelas telas de recebimento apresentadas.

## Funcionamento

O G-SUPRI registra o recebimento físico e envia um estado completo desse recebimento.
O SST resolve a obra, os produtos e suas unidades por vínculos previamente confirmados.
Materiais liberados entram automaticamente nos estoques existentes de EPI, EPC ou
uniforme. Itens sem vínculo ou sem liberação permanecem visíveis como pendências.
Um recebimento pode estar parcialmente aplicado. O SST não altera os dados do G-SUPRI.

Página: **Administração → Integração G-SUPRI** (`/#/administracao?aba=gsupri`).
Configuração e reprocessamento exigem administrador **com acesso global**.
Não é necessário digitar quantidades de estoque nessa página. O fator de embalagem e
os vínculos são configuração inicial, reaproveitada para os próximos recebimentos.

## Ativação e autenticação

Desabilitada por padrão. Configuração do servidor: `IntegracaoGsupri__Habilitada=true`.
Não existe botão de ativação nem credencial de integração no navegador.

1. Revisar/aplicar a migration `AdicionarIntegracaoGsupri` no ambiente de destino.
2. No app registration do SST, definir a App Role **Sst.ReceberEstoqueGSupri**,
   `allowedMemberTypes: [Application]`. Concedê-la somente ao service principal do G-SUPRI.
3. A equipe do G-SUPRI obtém token Entra ID via client credentials para a audiência do
   SST (`<Application-ID-URI-do-SST>/.default`). Tenant, audiência e credenciais reais
   dependem da configuração de implantação; não são publicados neste documento.
4. Configurar os vínculos de obras e produtos, testar em homologação e habilitar o envio.

O webhook exige token autenticado de aplicação e a App Role mesmo em desenvolvimento.
Token de usuário com escopo delegado não é aceito. A identidade do G-SUPRI não recebe
permissões de alterar vínculos ou consultar a página administrativa. Esta implementação
não provisionou recursos Azure nem concedeu permissões externas.

## Endpoint

`POST /api/integracoes/gsupri/v1/recebimentos`

`Authorization: Bearer <token>` e `Content-Type: application/json`.
Limite: 1 MiB e 500 itens por recebimento. Exemplo fictício:

```json
{
  "eventoId": "EV-2026-0001",
  "recebimentoId": "REC-0001",
  "versao": 1,
  "pedidoId": "PED-0001",
  "obraCodigo": "OBRA-001",
  "fornecedorDocumento": "12345678000190",
  "numeroNota": "4544",
  "chaveNfe": null,
  "recebidoEm": "2026-09-29T10:30:00-03:00",
  "cancelado": false,
  "itens": [
    {
      "itemId": "ITEM-1",
      "produtoCodigo": "LUVA-NITRILICA-M",
      "descricao": "Luva nitrilica M - caixa com 100 unidades",
      "unidade": "CX",
      "quantidadeRecebida": 2,
      "quantidadeLiberada": 1,
      "temNaoConformidade": true
    }
  ]
}
```

Com fator de conversão 100, este exemplo aplica **100 unidades** e deixa a outra
caixa pendente. A nota documenta a compra; a quantidade liberada no recebimento é
que determina o saldo disponível. Receber somente uma nota não confirma chegada física.

| Campo | Regra |
|---|---|
| eventoId | Identificador estável do envio; máximo 100 caracteres. Reenvios usam o mesmo ID. |
| recebimentoId | Identificador global e estável do recebimento físico; máximo 100. |
| versao | Inteiro positivo crescente por recebimento. Correções, liberações e cancelamentos incrementam a versão. |
| pedidoId | Referência do pedido; máximo 100. |
| obraCodigo | Código estável da obra na origem, vinculado previamente ao SST. Máximo 100. |
| fornecedorDocumento | Documento do fornecedor para rastreabilidade; até 20 caracteres. Não substitui validação fiscal. |
| numeroNota / chaveNfe | Número obrigatório (até 50), chave opcional com 44 dígitos. A chave não é deduplicador do recebimento. |
| recebidoEm | Data/hora real do recebimento, com fuso. |
| itemId | Estável por recebimento, até 100; não reutilizar para outro produto ou unidade. |
| produtoCodigo | Código **global do catálogo G-SUPRI**, incluindo a variante/tamanho, até 100. Se houver apenas código do fornecedor, o adaptador deve compor uma chave com fornecedor + código + variante. |
| unidade | Unidade recebida, até 10, vinculada ao fator de conversão. |
| descricao | Descrição original para conferência, até 500. Não usada como identificação automática. |
| quantidadeRecebida | Quantidade física desta parcela, decimal não negativo, até 6 casas. Não enviar o total acumulado do pedido. |
| quantidadeLiberada | Quantidade liberada, na mesma unidade, de zero até a recebida. Pode ser nula. |
| temNaoConformidade | Se verdadeiro e a liberação não vier informada, nenhuma quantidade deste item é presumida liberada. |
| cancelado | Verdadeiro estorna o que foi aplicado; aceita lista vazia de itens. |

IDs de evento, recebimento, obra, item e produto e a unidade são aparados e convertidos
para maiúsculas; devem ser únicos também após essa normalização. Ordem dos itens é irrelevante.
Outros campos fazem parte do conteúdo versionado. Para reenvio, preserve o payload.

## Consistência, revisões e cancelamentos

- Cada recebimento tem identificador próprio. Duas parcelas da mesma NF podem somar estoque.
- Reenvio idêntico retorna HTTP 200 e não reaplica quantidade.
- Novo evento com mesma versão/conteúdo também não duplica saldo.
- Mesmo evento ou mesma versão com conteúdo diferente: HTTP 409, sem alteração persistida.
- Versão anterior: HTTP 409; a origem deve consultar seu estado e reenviar uma versão atual.
- Uma nova versão traz o **estado completo**, não um incremento. O saldo muda apenas pela
  diferença em relação ao já aplicado. Omitir um item anteriormente aplicado solicita seu estorno.
- Não pode mudar a obra de um recebimento nem produto/unidade de um item. Use cancelamento
  e um novo recebimento, ou retire o item e acrescente outro ItemId numa versão superior.
- Cancelamentos e correções que causariam saldo negativo ficam em `PendenteRegularizacao`.
  Nenhum ajuste de saldo daquela revisão é aplicado. Após regularização, reprocessar.
- Cada evento mantém payload e hash; o recebimento guarda o estado mais recente. As
  movimentações identificam G-SUPRI, recebimento, revisão e NF; não se apaga histórico.
- Aplicação do saldo, marcação de quantidade aplicada e gravação do evento participam da
  mesma transação SQL serializável, com índices únicos e RowVersion dos estoques.
- Em concorrência, HTTP 409 pode pedir repetição do mesmo evento. Fazer tentativas com
  intervalo crescente. Após timeout ou 5xx, reenviar o mesmo evento/payload.
- HTTP 200 com pendência significa **persistido**, não necessariamente aplicado por inteiro.
  Falta de vínculo é resolvida no SST; qualidade e quantidades são corrigidas na origem.
- HTTP 400: payload inválido; 401/403: autenticação/autorização; 503: integração desabilitada.
  Não repetir indefinidamente erro de autenticação ou validação.

## Vínculos e unidades

`PUT /api/integracoes/gsupri/vinculos/obras`:
`{ "codigoExterno": "OBRA-001", "obraId": "<UUID-SST>" }`.

`PUT /api/integracoes/gsupri/vinculos/produtos`:
`{ "codigoExterno": "LUVA-NITRILICA-M", "unidade": "CX", "categoria": "EPI", "catalogoId": "<UUID-SST>", "tamanho": "", "fatorConversao": 100 }`.

Categorias: `EPI`, `EPC`, `UNIFORME`, `IGNORAR`. IGNORAR classifica explicitamente
materiais fora do escopo SST (ex.: aço), sem gerar estoque. Não há classificação por
palavras como “CA”, que poderiam confundir aço CA-50 com certificado de EPI.

Uniforme exige tamanho. EPI/EPC usam o catálogo existente: **cada variante de tamanho
deve ter um cadastro distinto**. A grade própria de tamanho de EPI não foi adicionada.
CA, fabricante e demais atributos do catálogo não são inventados a partir da nota.

A quantidade após conversão deve ser inteira: 0,5 caixa × 100 = 50; 0,001 caixa × 100
fica pendente, sem arredondamento. Os estoques atuais não suportam frações de unidade.

Vínculos são imutáveis nesta versão. Repetir o mesmo vínculo é permitido; trocar obra,
produto, tamanho ou fator retorna 409. Revise-os antes de salvar. Uma remediação de vínculo
incorreto requer procedimento controlado com análise do histórico, não alteração direta.

`GET /api/integracoes/gsupri/painel?pagina=1` retorna 25 recebimentos por página, vínculos
e estado de ativação. `POST /api/integracoes/gsupri/recebimentos/{id}/reprocessar` reavalia
um recebimento após resolução de pendências; é seguro repetir.

## Limites e dependências da outra equipe

- É necessário implementar o emissor/adaptador do G-SUPRI e pactuar os IDs e unidades.
- Esta entrega lê JSON estruturado. Não contém OCR, IA, consulta SEFAZ nem importador de
  XML bruto. O adaptador pode extrair o XML na origem; chave e número da NF acompanham
  os dados. O XML/PDF original permanece no G-SUPRI; não é baixado por URL fornecida.
- Não interpreta status fiscal automaticamente nem confirma autorização da NF-e na SEFAZ.
  A origem deve enviar apenas recebimentos elegíveis e transmitir correções/cancelamentos.
- Não há baixa automática de entregas aos trabalhadores a partir do G-SUPRI. Continuam
  valendo os fluxos e assinaturas de entrega existentes no SST.
- Não provisiona credenciais, não ativa a integração e não publica em produção.
- A homologação conjunta deve testar parcela, excesso, devolução/cancelamento, item com
  qualidade pendente, embalagem, variante, reenvio, timeout e revisão fora de ordem.

## Validação local

Testes de regras: `dotnet test tests/AAHBRANT.SST.Infrastructure.Tests --filter FullyQualifiedName~Gsupri`.
Os testes em memória validam regras; transações/concorrência precisam de SQL Server.
Build da API e TypeScript também devem passar antes da homologação conjunta.

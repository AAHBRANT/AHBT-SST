# Visualizador de documento (PDF) em PGR e PCMSO

Data: 2026-09-09

## Contexto e objetivo

Hoje, ao abrir um PGR ou um PCMSO já cadastrado (a partir da lista em `PgrsTab.tsx` /
`PcmsoTab.tsx`), o usuário cai direto num formulário de edição de campos estruturados
(inventário de riscos, plano de ação, dados clínicos etc.). Não existe nenhuma forma de
anexar ou visualizar o **documento PDF original** (o PGR ou o PCMSO de verdade, como
produzido/assinado fora do sistema).

O pedido: ao entrar no detalhe de um PGR ou de um PCMSO, o documento PDF já cadastrado
deve aparecer **aberto, por padrão, em tela cheia, com rolagem por todas as páginas** —
sem exigir clique extra em nenhum botão ou aba.

Descoberta feita durante o levantamento: não existe hoje **nenhum mecanismo de
upload/armazenamento de arquivo funcionando** no sistema.
- `PcmsoDetalhe.Arquivo` (string) foi criado para isso, mas nunca foi ligado a nenhuma
  tela, endpoint ou serviço — está sempre vazio (confirmado: nem o seeder de dados mock
  o preenche).
- `Evidencia` (entidade genérica, polimórfica via `EntidadeTipo`/`EntidadeId`, comentada
  como "usada por Aso, Treinamento, EntregaEpi e por qualquer módulo futuro") também
  nunca foi implementada de fato — não há coluna de conteúdo binário nem serviço de
  upload/download que a use.
- Não há Azure Blob Storage nem qualquer storage externo configurado no projeto.

Decisão (confirmada com o usuário): implementar o armazenamento **direto no banco de
dados** (sem depender de Azure Blob Storage), e usar o **visualizador nativo de PDF do
navegador** (iframe + Blob URL) em vez de uma biblioteca de renderização customizada
(ex. pdf.js). Ambas as escolhas priorizam simplicidade e zero infraestrutura nova, dado
o volume esperado (1 documento por PGR/PCMSO).

## Escopo

Incluído:
- Upload de um PDF por PGR e um PDF por PCMSO (reenviar substitui o anterior).
- Nova aba "Documento" em `PgrDetalhePage.tsx`, posicionada antes de "Inventário de
  riscos" e definida como aba padrão (primeira a abrir).
- Reestruturação de `PcmsoDetalhePage.tsx` (hoje sem abas) em duas abas: "Documento"
  (nova, padrão) e "Dados" (o formulário que já existe hoje, sem nenhuma mudança de
  conteúdo).
- Remoção do campo morto `PcmsoDetalhe.Arquivo` (confirmado com o usuário — nunca foi
  usado, sem risco de perda de dado real).
- Endpoints de upload (`POST`) e visualização (`GET`) do documento para PGR e PCMSO,
  reaproveitando a autorização RBAC já aplicada às rotas existentes de cada módulo.
- Teto de tamanho de upload: 20 MB.

Fora de escopo (não pedido, não fazer):
- Extração de texto/dados do PDF (OCR, parsing de conteúdo).
- Múltiplos documentos por PGR/PCMSO (versionamento/histórico de anexos) — é sempre 1
  documento vigente por registro, reenviar substitui.
- Reuso do mecanismo `Evidencia` por outros módulos (Aso, Treinamento, EntregaEpi) —
  fica fora deste trabalho, mesmo que a entidade seja genérica; não expandir escopo
  além do pedido.
- Azure Blob Storage — decisão explícita de não usar agora.

## Arquitetura

### Armazenamento

Reaproveitar a entidade `Evidencia` (`src/AAHBRANT.SST.Domain/Entidades/Evidencia.cs`),
que já é genérica e polimórfica (`EntidadeTipo` + `EntidadeId`), acrescentando uma
coluna binária:

```csharp
public class Evidencia : AuditableEntity
{
    // ... campos existentes (EntidadeTipo, EntidadeId, BlobUrl, NomeArquivo,
    // ContentType, HashSha256, AutorUsuarioId, Latitude, Longitude) ...

    public byte[]? Conteudo { get; set; }
}
```

- `EntidadeTipo` = `"Pgr"` ou `"Pcmso"`; `EntidadeId` = id do PGR/PCMSO.
- `Conteudo` guarda os bytes crus do PDF (coluna `varbinary(max)` no SQL Server).
- `BlobUrl` fica sem uso neste fluxo (era pensada para armazenamento externo, que não
  estamos usando agora) — não removida, por ser um campo genérico usado/reservado por
  outros cenários futuros da própria entidade; apenas não populada por este fluxo.
- `NomeArquivo` e `ContentType` continuam sendo preenchidos no upload (nome original do
  arquivo e `application/pdf`).
- Upload substitui: se já existir uma `Evidencia` com aquele `EntidadeTipo`/`EntidadeId`,
  ela é atualizada (não duplicada).

Migration nova:
1. Adicionar coluna `Conteudo` (`byte[]?`) em `Evidencia`.
2. Remover coluna `Arquivo` de `PcmsoDetalhe` (e o campo correspondente em
   `NovoPcmso`/`AtualizarPcmsoPayload` no DTO, e a referência em `PcmsoTab.tsx`
   `pcmsoVazio()` — hoje `arquivo: ''` já não é usada em lugar nenhum, então a remoção
   é só limpeza de código morto).

### Backend (API)

Novos endpoints, seguindo o mesmo padrão de autorização por policy já usado em
`PgrsController`/`PcmsosController` (`[Authorize(Policy = "pgr:editar")]` etc.):

```
POST /api/pgrs/{id}/documento     (multipart/form-data, campo "arquivo")  → policy "pgr:editar"
GET  /api/pgrs/{id}/documento                                            → policy "pgr:ver"
POST /api/pcmsos/{id}/documento   (multipart/form-data, campo "arquivo")  → policy "pcmso:editar"
GET  /api/pcmsos/{id}/documento                                          → policy "pcmso:ver"
```

- `POST`: valida `Content-Type` (deve ser PDF) e tamanho (máx. 20 MB); grava/atualiza a
  `Evidencia` correspondente via MediatR command (`EnviarDocumentoPgrCommand` /
  `EnviarDocumentoPcmsoCommand`, seguindo o padrão CQRS já usado em todo o projeto).
  Retorna `204 No Content` em sucesso, `400` se validação falhar.
- `GET`: retorna `404` se não houver documento ainda; caso exista, `return File(bytes,
  "application/pdf")` **sem** `fileDownloadName` (para não forçar
  `Content-Disposition: attachment` — não que isso afete o fluxo via Blob URL, mas
  mantém a semântica correta de "visualização", não "download").

### Frontend

Componente novo e compartilhado (usado pelas duas telas), ex.
`src/pages/shared/AbaDocumentoPdf.tsx`:

- Recebe `{ tipo: 'pgr' | 'pcmso', id: string, podeEditar: boolean }`.
- Ao montar, faz `fetch` autenticado (mesmo padrão `montarHeadersAuth()` já usado em
  `lib/api.ts` para os outros `/pdf` existentes) no `GET .../documento`.
  - `404` → mostra `EstadoVazio` ("Nenhum documento anexado ainda") com botão de
    upload (se `podeEditar`).
  - `200` → `blob = await response.blob()`, `URL.createObjectURL(blob)`, renderiza
    `<iframe src={blobUrl} style={{ width: '100%', height: '80vh', border: 'none' }}
    title="Documento" />`. O navegador (Chrome/Edge) já entrega rolagem contínua entre
    páginas, zoom e busca nativos.
  - Erro de rede → mensagem de erro dentro da aba, sem quebrar o resto da página.
- Botão "Substituir documento" (visível só se `podeEditar`) abre um seletor de arquivo
  (`<input type="file" accept="application/pdf">`), valida 20 MB no cliente antes de
  enviar (mesmo padrão de validação de tamanho já usado em `SeletorFotoCamera.tsx`),
  faz `POST` multipart, e recarrega o iframe após sucesso.
- `URL.revokeObjectURL` no cleanup do efeito, para não vazar memória ao trocar de
  aba/registro.

`PgrDetalhePage.tsx`:
- Novo valor de aba `'documento'` adicionado ao tipo `AbaPgr`.
- Nova `<Tab value="documento">Documento</Tab>` inserida **antes** de
  `<Tab value="inventario">`.
- `useState<AbaPgr>('documento')` como valor inicial (era `'inventario'`).

`PcmsoDetalhePage.tsx`:
- Introduzir `TabList`/`Tab` (que hoje não existem nesta tela), com duas abas:
  `'documento'` (padrão) e `'dados'`.
- Todo o JSX atual do formulário + plano de ação passa a renderizar apenas quando
  `aba === 'dados'`; nenhuma mudança de comportamento dentro dele.

## Tratamento de erros e casos de borda

- Documento ainda não enviado: estado vazio, não quebra a tela nem bloqueia acesso às
  outras abas.
- Upload > 20 MB: rejeitado no cliente (mensagem imediata) e validado de novo no
  servidor (defesa em profundidade, já que o cliente pode ser contornado).
- Upload de arquivo que não é PDF: rejeitado (client + server) por `Content-Type`.
- Usuário sem permissão de edição (`podeEditar = false`): visualiza o documento
  normalmente, mas não vê o botão de substituir.
- Falha de rede ao buscar o PDF: mensagem de erro local à aba, com opção de tentar
  novamente.

## Testes

- Backend: testes de integração dos 4 endpoints novos — upload cria/substitui,
  download retorna os bytes certos com `Content-Type: application/pdf`, 404 quando não
  há documento, 403 quando falta a policy, 400 quando arquivo não é PDF ou excede 20 MB.
  Seguem o padrão de teste já existente no projeto (mesma estrutura usada para os
  demais endpoints de PGR/PCMSO).
- Frontend: verificação visual no navegador (Browser pane) — subir um PDF de teste em
  um PGR e em um PCMSO, confirmar que a aba "Documento" abre por padrão, mostra o
  documento inteiro, permite rolar todas as páginas, e que trocar de aba e voltar não
  quebra o estado.

## Fora de escopo desta spec (não decidido aqui)

- Nenhum trabalho de deploy (Azure) é necessário para esta feature, já que a decisão
  foi armazenar no próprio banco — mas a migration ainda precisa ser aplicada em
  hml/produção quando o usuário autorizar o deploy (regra já registrada: nunca fazer
  deploy sem o usuário mandar).

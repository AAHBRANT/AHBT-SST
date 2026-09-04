# Rodapé de Rastreabilidade e Validação Digital — Design

**Data:** 2026-09-04
**Branch/worktree:** `integracao/deploy-treinamentos` — `.worktrees/reformulacao-treinamentos` (branch compartilhada com outra sessão Claude Code concorrente; sempre `git fetch` antes de commitar)
**Status:** aprovado em brainstorming, pronto para virar plano de implementação (`writing-plans`)

## 1. Objetivo

Padronizar o rodapé de todo PDF gerado pelo sistema (DDS, DDS Semanal, APR, PT, CIPA, Inspeção/Patrulha, Entrega/Ficha de EPI, Certificado de Treinamento, Ata de Sessão de Treinamento) com:

- Código de autenticidade (hash) e QR/link para validação pública do documento.
- Nota de assinatura digital (MP nº 2.200-2/2001 e Lei nº 14.063/2020), quando o documento de fato tem signatário registrado.
- Nome do sistema emissor, número do documento/protocolo.
- Paginação "Página X de Y".
- Data/hora de emissão e, quando existir no domínio, número de revisão.

Origem: pedido do usuário trazendo um checklist de campos de rodapé + exemplo de layout (texto corrido).

## 2. Escopo

Aplica-se a **todos os documentos operacionais** listados acima — não só aos que já passam pelo Motor de Assinatura Eletrônica. O comprovante de assinatura (`DocumentoAssinaturaPdfService`) já tem hash/QR próprios e não entra neste escopo (mantido como está).

## 3. Decisão central: reaproveitar o Motor de Assinatura para todos os documentos

Hoje `FinalizarDocumentoCommand` (que gera hash SHA-256 + token + QR e fecha o ciclo de assinatura) **exige pelo menos um signatário** e **não é idempotente** (lança erro se chamado de novo sobre um documento já finalizado). Isso inviabiliza reaproveitá-lo tal como está para documentos sem nenhum fluxo de assinatura (ex.: PGR).

**Decisão (Abordagem 1, escolhida sobre criar uma trilha paralela):** relaxar esse fluxo em vez de duplicá-lo.

- `FinalizarDocumentoCommand` passa a aceitar zero signatários — o hash nesse caso é calculado só a partir de `EntidadeTipo`/`EntidadeId` (sem dados de assinatura).
- Passa a ser **idempotente**: se o documento já está `Finalizado`, retorna o hash/token já existentes em vez de lançar `InvalidOperationException`.
- Um único conceito no sistema — "documento rastreável" — do qual "documento assinado" é um caso particular (quando `Signatarios.Count > 0`).
- A nota de assinatura digital (MP 2.200-2/Lei 14.063) só aparece no rodapé/página pública quando há signatário; sem signatário, mostra-se apenas o hash/QR de integridade, sem alegar assinatura.
- `ResolverDocumentoPublicoQuery`/`ValidacaoPublicaController` continuam funcionando sem alteração de contrato — já retornam `Signatarios` (lista vazia é um caso válido).

## 4. Quando gerar cada coisa

| O quê | Quando | Idempotência |
|---|---|---|
| **Protocolo/número do documento** | Na criação do registro (`Criar...Command`), mesmo padrão já usado por APR/PT/DDS Semanal/CIPA/PCMSO/Certificado | Gerado uma única vez, persistido na entidade |
| **Hash + token + QR de rastreabilidade** | Sob demanda, no primeiro export do PDF (`Exportar...PdfQuery`) | `IRegistradorRastreabilidadeService.GarantirAsync` — reaproveita se já existir |

Justificativa de gerar hash/token sob demanda (não na criação): evita rastreabilidade para registros que nunca chegam a virar PDF (rascunhos, DDS nunca baixado). Protocolo continua na criação porque já é referenciado em listagens/UI antes de qualquer export.

## 5. Numeração automática — o que falta

Infra já existe e funciona (`IGeradorNumeroDocumentoService`/`GeradorNumeroDocumentoService`/`ContadorDocumento`, contador por prefixo+ano, formato `PREFIXO-ANO-NNNN`). Já plugada em: APR (`APR`), PT (`PT`), DDS Semanal (`DDS`), CIPA/Edital (`CIPA-EDITAL`), PCMSO (`PCMSO`), Certificado de Treinamento (`CERT`).

Falta adicionar (novo campo `NumeroDocumento` nullable + chamada de `GerarAsync` no `Criar...Command` respectivo):

| Documento | Entidade/campo novo | Prefixo |
|---|---|---|
| DDS (diário) | `Dds.NumeroDocumento` | `DDS-D` (prefixo `DDS` já pertence ao DDS Semanal) |
| PGR | `Pgr.NumeroDocumento` | `PGR` |
| Inspeção/Patrulha | `Inspecao.NumeroDocumento` | `INSP` |
| Entrega/Ficha de EPI | `EntregaEpi.NumeroDocumento` | `EPI` |

Nota: `EntregaEpi.NumeroListaPresencaNr6` é um campo **manual, digitado pelo usuário** (número externo da lista de presença NR-6) — conceito diferente, não reaproveitado aqui.

Ata de Sessão de Treinamento reaproveita `SessaoTreinamento.NumeroCertificado`, já existente.

## 6. Revisão/Versão

Só exibida quando o domínio já tem esse conceito — hoje, só PGR (`PgrRevisao.NumeroRevisao`). Nos demais tipos (documentos de evento único: DDS, APR, PT, CIPA, Inspeção, Entrega de EPI, Certificado), a linha de revisão simplesmente não aparece no rodapé.

## 7. Componente de rodapé compartilhado

Novo `RodapeDocumentoPadrao.Desenhar(...)`, ao lado de `CabecalhoDocumentoPadrao.cs` (`src/AAHBRANT.SST.Infrastructure/Documentos/`), usado por todos os serviços de PDF no lugar do atual `pagina.Footer().AlignCenter().Text("Gerado em ...")`.

Layout (3-4 linhas centralizadas + QR pequeno):

```
AAHBRANT SST | DDS nº DDS-D-2026-0842  [— Revisão N, só quando existir]
Documento assinado digitalmente conforme MP nº 2.200-2/2001 e Lei nº 14.063/2020.   [só quando TemAssinatura]
Validável em https://.../#/validar/{token} — chave A1B2-C3D4 | Emitido em 04/09/2026 às 12:40
Página 1 de 3
```

- **Chave curta**: 8 primeiros caracteres do hash SHA-256, maiúsculos, formato `XXXX-XXXX` — atalho visual; a conferência de fato acontece via QR/link, que carrega o token completo.
- **QR code**: pequeno, reaproveita `QrCodeDocumentoService` (já existe, sem alteração).
- **Página X de Y**: nativo do QuestPDF (`text.CurrentPageNumber()`/`text.TotalPages()`).
- **Nome do sistema emissor**: fixo, `"AAHBRANT SST"`.

Parâmetros do `Modelo` de cada PDF ganham: `Protocolo` (string?), `ConteudoHash` (string), `TokenValidacaoPublica` (string), `TemAssinatura` (bool), `Revisao` (int?, só PGR preenche).

## 8. Fluxo por serviço de PDF

Cada `Exportar...PdfQuery` (Dds, DdsSemanal, Apr, Pt, Cipa, Inspecao, EntregaEpi, e no worktree de Treinamentos: Certificado, Ata Sessão) passa a:

1. Chamar `IRegistradorRastreabilidadeService.GarantirAsync(entidadeTipo, entidadeId, ct)` — find-or-create de `DocumentoAssinatura` (reaproveita `CriarDocumentoAssinaturaCommand`) + gera/reaproveita hash+token+QR via a versão relaxada de `FinalizarDocumentoCommand`.
2. Montar o `Modelo` do PDF com os novos campos.
3. O `...PdfService.cs` correspondente troca o footer atual pela chamada a `RodapeDocumentoPadrao.Desenhar(...)`.

## 9. Testes

- Unitário: `GarantirAsync` chamado duas vezes no mesmo documento retorna o mesmo hash/token (idempotência).
- Unitário: `FinalizarDocumentoCommand` com zero signatários não lança mais `InvalidOperationException` e gera hash válido.
- Unitário: `GeradorNumeroDocumentoService` respeita contador por prefixo+ano (dois documentos seguidos do mesmo prefixo/ano incrementam; ano diferente reinicia).
- Verificação visual: exportar um PDF de cada tipo (os 4 novos tipos com numeração + pelo menos 1 tipo que já tinha) e conferir rodapé via extração de texto (pypdf — não há Poppler no ambiente, técnica já usada antes nesta sessão/projeto).

## 10. Fora de escopo (não mexer)

- `DocumentoAssinaturaPdfService` (comprovante de assinatura em si) — já tem hash/QR próprios, não é tocado.
- Fluxo de assinatura biométrica/sessão logada — inalterado, só a guarda de "zero signatários" em `FinalizarDocumentoCommand` muda.
- Qualquer trabalho do repositório raiz (`master`) — fora desta branch/worktree.

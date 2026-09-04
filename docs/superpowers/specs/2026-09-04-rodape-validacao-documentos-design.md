# Rodapé de Rastreabilidade e Validação Digital — Design

**Data:** 2026-09-04
**Branch/worktree:** `integracao/deploy-treinamentos` — `.worktrees/reformulacao-treinamentos` (branch compartilhada com outra sessão Claude Code concorrente; sempre `git fetch` antes de commitar)
**Status:** aprovado em brainstorming, pronto para virar plano de implementação (`writing-plans`)

## 1. Objetivo

Padronizar o rodapé de todo PDF gerado pelo sistema (DDS, DDS Semanal, APR, PT, CIPA, Inspeção/Patrulha, Entrega/Ficha de EPI, Certificado de Treinamento, Ata de Sessão de Treinamento) com:

**PGR está fora do escopo**: não tem `PgrPdfService`/`ExportarPgrPdfQuery` nenhum hoje — construir essa exportação do zero seria um recurso separado. CIPA e DDS Semanal seguem como exemplos reais de documento sem assinatura digital (linha em branco para assinatura física), então o caso "zero signatários" continua coberto sem o PGR.

- Código de autenticidade (hash) e QR/link para validação pública do documento.
- Nota de assinatura digital (MP nº 2.200-2/2001 e Lei nº 14.063/2020), quando o documento de fato tem signatário registrado.
- Nome do sistema emissor, número do documento/protocolo.
- Paginação "Página X de Y".
- Data/hora de emissão e, quando existir no domínio, número de revisão.

Origem: pedido do usuário trazendo um checklist de campos de rodapé + exemplo de layout (texto corrido).

## 2. Escopo

Aplica-se a **todos os documentos operacionais** listados acima — não só aos que já passam pelo Motor de Assinatura Eletrônica. O comprovante de assinatura (`DocumentoAssinaturaPdfService`) já tem hash/QR próprios e não entra neste escopo (mantido como está).

## 3. Decisão central: rastreabilidade separada de finalização (correção pós-leitura de código)

**Versão original desta seção (revisada):** a primeira versão deste design propunha relaxar `FinalizarDocumentoCommand` para aceitar zero signatários e virar idempotente, reaproveitando-o para todo documento. **Isso foi corrigido** ao ler `RegistradorAssinaturaService.cs`: `FinalizarDocumentoCommand` não só gera hash — ele também fecha o documento para novas assinaturas (`Status = Finalizado`), e `RegistradorAssinaturaService` bloqueia qualquer assinatura nova assim que `Status != EmAndamento`. Chamar isso no primeiro export de PDF travaria, por exemplo, um DDS exportado no meio do dia (antes de todos assinarem a presença biométrica) — ninguém mais conseguiria assinar depois. Regressão real na funcionalidade "assinatura automática pela presença" implementada nesta mesma branch.

**Decisão corrigida:** rastreabilidade (hash+token+QR) e finalização (fechar o ciclo de assinatura) são conceitos **separados**, cada um com seu próprio disparador. O caso "documento sem nenhum signatário" não é uma exceção rara — é o estado normal de qualquer documento exportado antes (ou sem nunca ter) fluxo de assinatura completo (ex.: um APR exportado antes da liberação ser assinada; CIPA e DDS Semanal, que nunca assinam digitalmente):

- **`FinalizarDocumentoCommand` não muda em nada** — continua exigindo ≥1 signatário, continua fechando o documento e gerando o comprovante, disparado só pelas ações de negócio que já o chamam hoje (ex.: `EncerrarSessaoTreinamentoCommand`).
- **Novo `IRegistradorRastreabilidadeService.GarantirAsync(entidadeTipo, entidadeId, ct)`** (mesmo padrão de arquivo/DI de `IRegistradorAssinaturaService.cs`: interface + implementação no mesmo arquivo, injeta `IAppDbContext` direto, sem passar por `IMediator`): find-or-create de `DocumentoAssinatura`; gera `TokenValidacaoPublica` uma única vez (se ainda não existir); recalcula `ConteudoHash` a cada chamada **enquanto `Status == EmAndamento`** (reflete os signatários até agora, inclusive zero); **não mexe em `Status` nem em `FinalizadoEm`**. Se o documento já está `Finalizado`, apenas devolve o hash/token já congelados por `FinalizarDocumentoCommand` (sem recalcular).
- Novo campo `DocumentoAssinatura.RastreadoEm` (`DateTime?`): timestamp de quando o token foi gerado pela primeira vez — usado como "emitido em" nos documentos que nunca chegam a ser finalizados (ex.: CIPA e DDS Semanal, que não têm fluxo de assinatura digital).
- A nota de assinatura digital (MP 2.200-2/Lei 14.063) só aparece no rodapé/página pública quando `Signatarios.Count > 0`; sem signatário, mostra-se apenas o hash/QR de integridade, sem alegar assinatura.

## 4. Página pública de validação — ajuste necessário

`ResolverDocumentoPublicoQueryHandler` hoje só resolve `Status == Finalizado` e usa `documento.FinalizadoEm!.Value` (null-forgiving). Documentos que nunca finalizam (CIPA, DDS Semanal) ou que ainda estão `EmAndamento` no momento em que alguém escaneia o QR (DDS/APR/PT no meio do fluxo) resultariam em 404 ou `NullReferenceException`. Ajuste:

- Filtro passa a ser só `TokenValidacaoPublica == token` (sem exigir `Status == Finalizado`).
- `DocumentoPublicoDto` troca `FinalizadoEm` (não-nulo) por `EmitidoEm` (`FinalizadoEm ?? RastreadoEm ?? Ativo em`, sempre terá um dos dois preenchido nesse ponto) e ganha `Assinado` (bool, `Signatarios.Count > 0`) para a página distinguir "documento rastreável, ainda sem assinatura" de "documento assinado digitalmente".

## 5. Quando gerar cada coisa

| O quê | Quando | Idempotência |
|---|---|---|
| **Protocolo/número do documento** | Na criação do registro (`Criar...Command`), mesmo padrão já usado por APR/PT/DDS Semanal/CIPA/PCMSO/Certificado | Gerado uma única vez, persistido na entidade |
| **Token de validação pública** | Sob demanda, no primeiro export do PDF (`Exportar...PdfQuery`) | Gerado uma única vez, persistido em `DocumentoAssinatura.TokenValidacaoPublica` |
| **Hash de integridade** | Recalculado a cada export enquanto `EmAndamento`; congelado quando `Finalizado` (por `FinalizarDocumentoCommand`, inalterado) | N/A — sempre reflete os signatários atuais até finalizar |

Justificativa de gerar rastreabilidade sob demanda (não na criação): evita gerar token para registros que nunca chegam a virar PDF (rascunhos, DDS nunca baixado). Protocolo continua na criação porque já é referenciado em listagens/UI antes de qualquer export.

## 6. Numeração automática — o que falta

Infra já existe e funciona (`IGeradorNumeroDocumentoService`/`GeradorNumeroDocumentoService`/`ContadorDocumento`, contador por prefixo+ano, formato `PREFIXO-ANO-NNNN`). Já plugada em: APR (`APR`), PT (`PT`), DDS Semanal (`DDS`), CIPA/Edital (`CIPA-EDITAL`), PCMSO (`PCMSO`), Certificado de Treinamento (`CERT`).

Falta adicionar (novo campo `NumeroDocumento` nullable + chamada de `GerarAsync` no `Criar...Command` respectivo):

| Documento | Entidade/campo novo | Prefixo |
|---|---|---|
| DDS (diário) | `Dds.NumeroDocumento` | `DDS-D` (prefixo `DDS` já pertence ao DDS Semanal) |
| Inspeção/Patrulha | `Inspecao.NumeroDocumento` | `INSP` |
| Entrega/Ficha de EPI | `EntregaEpi.NumeroDocumento` | `EPI` |

Nota: `EntregaEpi.NumeroListaPresencaNr6` é um campo **manual, digitado pelo usuário** (número externo da lista de presença NR-6) — conceito diferente, não reaproveitado aqui.

Ata de Sessão de Treinamento reaproveita `SessaoTreinamento.NumeroCertificado`, já existente.

## 7. Revisão/Versão

Nenhum dos 8 tipos em escopo tem conceito de revisão hoje (todos são documentos de evento único: DDS, DDS Semanal, APR, PT, CIPA, Inspeção, Entrega de EPI, Certificado/Ata). O campo `Revisao` (int?) fica no `Modelo` do PDF e no `RodapeDocumentoPadrao` para o caso geral, mas nunca é preenchido nesta rodada — a linha correspondente simplesmente não aparece. Fica pronto para o dia em que o PGR ganhar seu próprio PDF (fora deste escopo) e reaproveitar `PgrRevisao.NumeroRevisao`.

## 8. Componente de rodapé compartilhado

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

Parâmetros do `Modelo` de cada PDF ganham: `Protocolo` (string?), `ConteudoHash` (string), `TokenValidacaoPublica` (string), `TemAssinatura` (bool), `Revisao` (int?, nenhum tipo em escopo preenche por ora — ver seção 7).

## 9. Fluxo por serviço de PDF

Cada `Exportar...PdfQuery` (Dds, DdsSemanal, Apr, Pt, Cipa, Inspecao, EntregaEpi, e no worktree de Treinamentos: Certificado, Ata Sessão) passa a:

1. Chamar `IRegistradorRastreabilidadeService.GarantirAsync(entidadeTipo, entidadeId, ct)` — find-or-create de `DocumentoAssinatura` (mesma lógica de `CriarDocumentoAssinaturaCommand`, sem status change) + garante token (gera se ainda não existir) + recalcula/reaproveita hash conforme a seção 3 + gera o PNG do QR via `IQrCodeDocumentoService.Gerar(token)`.
2. Montar o `Modelo` do PDF com os novos campos.
3. O `...PdfService.cs` correspondente troca o footer atual pela chamada a `RodapeDocumentoPadrao.Desenhar(...)`.

## 10. Testes

- Unitário: `GarantirAsync` chamado duas vezes seguidas sobre um documento `EmAndamento` sem signatários devolve o mesmo token, mas recalcula o hash (comportamento esperado, não é bug).
- Unitário: `GarantirAsync` sobre um documento já `Finalizado` devolve o hash/token exatamente como `FinalizarDocumentoCommand` os deixou, sem alterá-los.
- Unitário: `FinalizarDocumentoCommand` continua exigindo ≥1 signatário e continua lançando erro se chamado de novo (comportamento **inalterado** — não é mais tocado por esta feature).
- Unitário: `ResolverDocumentoPublicoQueryHandler` resolve um documento `EmAndamento` (sem `FinalizadoEm`) usando `RastreadoEm` como `EmitidoEm`, e um documento `Finalizado` continua funcionando como antes.
- Unitário: `GeradorNumeroDocumentoService` respeita contador por prefixo+ano (dois documentos seguidos do mesmo prefixo/ano incrementam; ano diferente reinicia).
- Verificação visual: exportar um PDF de cada tipo (os 4 novos tipos com numeração + pelo menos 1 tipo que já tinha) e conferir rodapé via extração de texto (pypdf — não há Poppler no ambiente, técnica já usada antes nesta sessão/projeto).

## 11. Fora de escopo (não mexer)

- `DocumentoAssinaturaPdfService` (comprovante de assinatura em si) — já tem hash/QR próprios, não é tocado.
- `FinalizarDocumentoCommand` e todo o fluxo de assinatura biométrica/sessão logada — **inalterados**; a correção da seção 3 eliminou a necessidade de mexer neles.
- Qualquer trabalho do repositório raiz (`master`) — fora desta branch/worktree.

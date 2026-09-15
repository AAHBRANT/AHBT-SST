using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AAHBRANT.SST.Infrastructure.Documentos;

// Rodapé padrão de rastreabilidade/validação digital aplicado a todo documento gerado — protocolo,
// hash+QR de validação pública (Motor de Assinatura Eletrônica, via IRegistradorRastreabilidadeService,
// que nunca depende de o documento estar Finalizado), nota de assinatura digital (só quando há
// signatário real) e paginação. Ver docs/superpowers/specs/2026-09-04-rodape-validacao-documentos-design.md.
internal static class RodapeDocumentoPadrao
{
    public static void Desenhar(
        ColumnDescriptor coluna,
        string tituloDocumento,
        string? protocolo,
        int? revisao,
        string conteudoHash,
        string urlValidacaoPublica,
        byte[] qrCodePng,
        bool temAssinatura)
    {
        coluna.Item().PaddingTop(4).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
        coluna.Item().PaddingTop(2).Row(linha =>
        {
            linha.RelativeItem().Column(textoColuna =>
            {
                textoColuna.Item().AlignCenter().Text(t =>
                {
                    t.Span("AAHBRANT SST").FontSize(7).SemiBold();
                    // O protocolo já embute o prefixo do tipo de documento (ex.: "APR-2026-0001",
                    // "DDS-D-2026-0007") — mostrar "{tituloDocumento} nº {protocolo}" ficava redundante
                    // ("APR nº APR-2026-0001"). Sem protocolo (documentos com chave sintética, sem
                    // numeração automática), tituloDocumento sozinho ainda identifica o tipo.
                    if (!string.IsNullOrEmpty(protocolo))
                        t.Span($" | {protocolo}").FontSize(7);
                    else
                        t.Span($" | {tituloDocumento}").FontSize(7);
                    if (revisao is not null)
                        t.Span($" — Revisão {revisao}").FontSize(7);
                });

                if (temAssinatura)
                {
                    textoColuna.Item().AlignCenter()
                        .Text("Documento assinado digitalmente conforme MP nº 2.200-2/2001 e Lei nº 14.063/2020.")
                        .FontSize(6.5f).Italic();
                }

                // Centralizado (revertido a pedido do usuário, 04/09 — a tentativa de alinhar à
                // direita/justificado "colado" no QR não ficou boa e ele pediu de volta o centralizado
                // original).
                textoColuna.Item().AlignCenter().Text(t =>
                {
                    // Chave curta: atalho visual (8 primeiros caracteres do hash SHA-256, maiúsculos,
                    // formatado XXXX-XXXX) — a conferência de fato acontece pelo QR/link, que carrega
                    // o token completo, não pelo hash em si.
                    var chaveCurta = conteudoHash.Length >= 8 ? $"{conteudoHash[..4]}-{conteudoHash[4..8]}" : conteudoHash;
                    t.Span($"Validável em {urlValidacaoPublica} — chave {chaveCurta} | Emitido em ").FontSize(6.5f);
                    t.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(6.5f);
                });

                textoColuna.Item().AlignCenter().Text(t =>
                {
                    t.Span("Página ").FontSize(6.5f);
                    t.CurrentPageNumber().FontSize(6.5f);
                    t.Span(" de ").FontSize(6.5f);
                    t.TotalPages().FontSize(6.5f);
                });
            });

            // 55pt (~1,9cm) — reduzido de 70pt (pedido do usuário, 04/09) porque o rodapé mais alto
            // estava sobrando pouco espaço de conteúdo em documentos mais longos (ex.: APR foi pra 2
            // páginas). Ainda bem acima do 28pt original (ilegível) — verificado com decodificação
            // real (OpenCV) a 144 DPI antes de aplicar.
            linha.ConstantItem(55).AlignRight().Image(qrCodePng).FitArea();
        });
    }
}

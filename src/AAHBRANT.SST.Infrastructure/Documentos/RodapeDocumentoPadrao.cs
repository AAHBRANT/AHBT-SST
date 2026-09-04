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
                    if (!string.IsNullOrEmpty(protocolo))
                        t.Span($" | {tituloDocumento} nº {protocolo}").FontSize(7);
                    if (revisao is not null)
                        t.Span($" — Revisão {revisao}").FontSize(7);
                });

                if (temAssinatura)
                {
                    textoColuna.Item().AlignCenter()
                        .Text("Documento assinado digitalmente conforme MP nº 2.200-2/2001 e Lei nº 14.063/2020.")
                        .FontSize(6.5f).Italic();
                }

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

            // 70pt (~2,5cm) — abaixo disso a câmera do celular não consegue focar/resolver os módulos
            // do QR numa URL longa como a nossa (~115 caracteres), mesmo impresso em boa qualidade.
            linha.ConstantItem(70).AlignRight().Image(qrCodePng).FitArea();
        });
    }
}

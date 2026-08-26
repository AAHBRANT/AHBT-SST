using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AAHBRANT.SST.Infrastructure.Assinatura;

// Mesmo padrão visual de DdsPdfService (branding AAHBRANT #670000) — mas layout próprio, porque este
// é um comprovante de assinatura genérico (aplicável a qualquer módulo), não uma reprodução do DDS.
public class DocumentoAssinaturaPdfService : IDocumentoAssinaturaPdfService
{
    private const string CorMarca = "#670000";

    public byte[] Gerar(DocumentoAssinaturaPdfModelo modelo)
    {
        var documento = Document.Create(container =>
        {
            container.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(2, Unit.Centimetre);
                pagina.DefaultTextStyle(estilo => estilo.FontSize(11));

                pagina.Header().Column(coluna =>
                {
                    coluna.Item().Text("AAHBRANT").FontSize(18).Bold().FontColor(CorMarca);
                    coluna.Item().Text("Comprovante de Assinatura Eletrônica").FontSize(14).SemiBold();
                    coluna.Item().PaddingTop(4).LineHorizontal(2).LineColor(CorMarca);
                });

                pagina.Content().PaddingVertical(12).Column(coluna =>
                {
                    coluna.Spacing(8);

                    coluna.Item().Text(t =>
                    {
                        t.Span("Documento: ").SemiBold();
                        t.Span($"{modelo.EntidadeTipo} #{modelo.EntidadeId}");
                    });
                    coluna.Item().Text(t =>
                    {
                        t.Span("Finalizado em: ").SemiBold();
                        t.Span(modelo.FinalizadoEm.ToString("dd/MM/yyyy HH:mm"));
                    });

                    coluna.Item().PaddingTop(8).Text("Assinaturas registradas").FontSize(13).Bold();
                    foreach (var signatario in modelo.Signatarios)
                    {
                        coluna.Item().Text(t =>
                        {
                            t.Span($"• {signatario.TrabalhadorNome} — ").SemiBold();
                            t.Span($"{DescreverMetodo(signatario.Metodo)}, em {signatario.AssinadoEm:dd/MM/yyyy HH:mm}");
                        });
                    }

                    coluna.Item().PaddingTop(12).Text("Este comprovante atesta que os registros acima foram assinados eletronicamente, com validade jurídica conforme MP 2.200-2/2001, art. 10, §2º.").FontSize(9).Italic();

                    coluna.Item().PaddingTop(8).Text(t =>
                    {
                        t.Span("Hash de integridade (SHA-256): ").SemiBold().FontSize(9);
                        t.Span(modelo.ConteudoHash).FontSize(9);
                    });

                    if (modelo.QrCodePng is not null)
                    {
                        coluna.Item().PaddingTop(12).Column(qrColuna =>
                        {
                            qrColuna.Item().Text("Validar este documento").FontSize(10).SemiBold();
                            qrColuna.Item().PaddingTop(4).Width(3, Unit.Centimetre).Image(modelo.QrCodePng);
                            if (modelo.UrlValidacaoPublica is not null)
                                qrColuna.Item().PaddingTop(2).Text(modelo.UrlValidacaoPublica).FontSize(8);
                        });
                    }
                });

                pagina.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Gerado em ").FontSize(9);
                    t.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(9);
                });
            });
        });

        return documento.GeneratePdf();
    }

    private static string DescreverMetodo(MetodoAutenticacaoAssinatura metodo) => metodo switch
    {
        MetodoAutenticacaoAssinatura.Biometria => "Biometria (leitor da obra)",
        MetodoAutenticacaoAssinatura.CrachaPin => "Crachá + PIN",
        MetodoAutenticacaoAssinatura.QrCodePin => "QR Code + PIN",
        MetodoAutenticacaoAssinatura.WebAuthnCelular => "Biometria (celular)",
        _ => metodo.ToString(),
    };
}

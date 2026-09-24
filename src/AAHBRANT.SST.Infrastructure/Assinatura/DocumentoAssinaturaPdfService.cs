using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Documentos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AAHBRANT.SST.Infrastructure.Assinatura;

// Mesmo padrão visual dos demais documentos (CabecalhoDocumentoPadrao, branding AAHBRANT #670000)
// — mas sem a logomarca da obra: este comprovante é do Motor de Assinatura Eletrônica, decoupled de
// cada módulo (só conhece EntidadeTipo/EntidadeId, não a obra de origem — ver comentário no modelo).
public class DocumentoAssinaturaPdfService : IDocumentoAssinaturaPdfService
{
    public byte[] Gerar(DocumentoAssinaturaPdfModelo modelo)
    {
        var documento = Document.Create(container =>
        {
            container.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(2, Unit.Centimetre);
                pagina.DefaultTextStyle(estilo => estilo.FontSize(11));

                // O motor não conhece a obra de origem (só EntidadeTipo/EntidadeId), e o cabeçalho
                // padrão caía em "Obra não identificada" — o que num comprovante enviado a cliente ou
                // fiscalização parece falha de emissão. No lugar do nome da obra vai o rótulo do
                // documento a que este comprovante se refere.
                pagina.Header().Column(coluna =>
                    CabecalhoDocumentoPadrao.Desenhar(coluna, "Comprovante de Assinatura Eletrônica",
                        obraNome: TipoDocumentoAssinatura.Rotulo(modelo.EntidadeTipo), logoConteudo: null));

                pagina.Content().PaddingVertical(12).Column(coluna =>
                {
                    coluna.Spacing(8);

                    coluna.Item().Text(t =>
                    {
                        t.Span("Documento: ").SemiBold();
                        t.Span($"{TipoDocumentoAssinatura.Rotulo(modelo.EntidadeTipo)} #{modelo.EntidadeId}");
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
                    t.Span(HorarioBrasilia.Agora.ToString("dd/MM/yyyy HH:mm")).FontSize(9);
                });
            });
        });

        return documento.GeneratePdf();
    }

    private static string DescreverMetodo(MetodoAutenticacaoAssinatura metodo) => metodo switch
    {
        MetodoAutenticacaoAssinatura.Biometria => "Digital (leitor Futronic FS80H)",
        MetodoAutenticacaoAssinatura.SessaoLogada => "Sessão logada",
        // Faltava desde que o método facial foi criado (04/09): sem este caso, o comprovante impresso
        // exibia o nome cru do enum ("ReconhecimentoFacial") no lugar do rótulo institucional.
        MetodoAutenticacaoAssinatura.ReconhecimentoFacial => "Reconhecimento facial (Azure Face API)",
        _ => metodo.ToString(),
    };
}

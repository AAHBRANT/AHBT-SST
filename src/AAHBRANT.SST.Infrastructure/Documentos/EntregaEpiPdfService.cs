using AAHBRANT.SST.Application.EntregasEpi;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AAHBRANT.SST.Infrastructure.Documentos;

public class EntregaEpiPdfService : IEntregaEpiPdfService
{
    private const string CorMarca = "#670000";

    public byte[] Gerar(EntregaEpiPdfModelo modelo)
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
                    coluna.Item().Text(modelo.ObraNome).FontSize(18).Bold().FontColor(CorMarca);
                    coluna.Item().Text("Ficha de Controle e Entrega de EPI").FontSize(14).SemiBold();
                    coluna.Item().PaddingTop(4).LineHorizontal(2).LineColor(CorMarca);
                });

                pagina.Content().PaddingVertical(12).Column(coluna =>
                {
                    coluna.Spacing(8);

                    coluna.Item().Text("Trabalhador").FontSize(13).Bold();
                    coluna.Item().Row(linha =>
                    {
                        linha.RelativeItem(2).Text(t =>
                        {
                            t.Span("Nome: ").SemiBold();
                            t.Span(modelo.TrabalhadorNome);
                        });
                        linha.RelativeItem().Text(t =>
                        {
                            t.Span("Matrícula: ").SemiBold();
                            t.Span(modelo.TrabalhadorMatricula);
                        });
                    });
                    coluna.Item().Text(t =>
                    {
                        t.Span("Função: ").SemiBold();
                        t.Span(modelo.TrabalhadorFuncaoNome);
                    });

                    coluna.Item().PaddingTop(8).Text("EPI").FontSize(13).Bold();
                    coluna.Item().Row(linha =>
                    {
                        linha.RelativeItem(2).Text(t =>
                        {
                            t.Span("Descrição: ").SemiBold();
                            t.Span(modelo.EpiNome);
                        });
                        linha.RelativeItem().Text(t =>
                        {
                            t.Span("Fabricante: ").SemiBold();
                            t.Span(modelo.EpiFabricante ?? "-");
                        });
                    });
                    coluna.Item().Text(t =>
                    {
                        t.Span("CA: ").SemiBold();
                        t.Span(modelo.CertificadoAprovacaoNumero ?? "-");
                    });

                    coluna.Item().PaddingTop(8).Text("Entrega").FontSize(13).Bold();
                    coluna.Item().Row(linha =>
                    {
                        linha.RelativeItem().Text(t =>
                        {
                            t.Span("Data da entrega: ").SemiBold();
                            t.Span(modelo.DataEntrega.ToString("dd/MM/yyyy"));
                        });
                        linha.RelativeItem().Text(t =>
                        {
                            t.Span("Quantidade: ").SemiBold();
                            t.Span(modelo.Quantidade.ToString());
                        });
                        linha.RelativeItem().Text(t =>
                        {
                            t.Span("Validade: ").SemiBold();
                            t.Span(modelo.DataValidade?.ToString("dd/MM/yyyy") ?? "-");
                        });
                    });
                    coluna.Item().Text(t =>
                    {
                        t.Span("Motivo: ").SemiBold();
                        t.Span(modelo.Motivo ?? "-");
                    });

                    if (modelo.DataDevolucao is not null)
                    {
                        coluna.Item().PaddingTop(8).Text("Devolução").FontSize(13).Bold();
                        coluna.Item().Row(linha =>
                        {
                            linha.RelativeItem().Text(t =>
                            {
                                t.Span("Data da devolução: ").SemiBold();
                                t.Span(modelo.DataDevolucao?.ToString("dd/MM/yyyy") ?? "-");
                            });
                            linha.RelativeItem().Text(t =>
                            {
                                t.Span("Quantidade devolvida: ").SemiBold();
                                t.Span(modelo.QuantidadeDevolucao?.ToString() ?? "-");
                            });
                        });
                    }

                    coluna.Item().PaddingTop(8).Text(t =>
                    {
                        t.Span("Visto do responsável: ").SemiBold();
                        t.Span(modelo.VistoConsorcioResponsavel ?? "-");
                    });

                    if (!string.IsNullOrWhiteSpace(modelo.Observacoes))
                    {
                        coluna.Item().PaddingTop(8).Text("Observações").FontSize(13).Bold();
                        coluna.Item().Text(modelo.Observacoes);
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
}

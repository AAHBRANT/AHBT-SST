using AAHBRANT.SST.Application.Dds;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AAHBRANT.SST.Infrastructure.Documentos;

public class DdsPdfService : IDdsPdfService
{
    private const string CorMarca = "#670000";

    public byte[] Gerar(DdsPdfModelo modelo)
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
                    coluna.Item().Text("DDS — Diálogo Diário de Segurança").FontSize(14).SemiBold();
                    coluna.Item().PaddingTop(4).LineHorizontal(2).LineColor(CorMarca);
                });

                pagina.Content().PaddingVertical(12).Column(coluna =>
                {
                    coluna.Spacing(8);

                    coluna.Item().Text(modelo.TopicoPrincipal).FontSize(16).Bold();

                    coluna.Item().Row(linha =>
                    {
                        linha.RelativeItem().Text(t =>
                        {
                            t.Span("Obra: ").SemiBold();
                            t.Span(modelo.ObraNome);
                        });
                        linha.RelativeItem().Text(t =>
                        {
                            t.Span("Data: ").SemiBold();
                            t.Span(modelo.Data.ToString("dd/MM/yyyy"));
                        });
                    });

                    coluna.Item().Text(t =>
                    {
                        t.Span("Responsável: ").SemiBold();
                        t.Span(modelo.ResponsavelNome);
                    });

                    coluna.Item().Text(t =>
                    {
                        t.Span("Atividades do dia: ").SemiBold();
                        t.Span(string.Join(", ", modelo.AtividadesNomes));
                    });

                    coluna.Item().PaddingTop(8).Text("Checklist de verificação").FontSize(13).Bold();
                    foreach (var item in modelo.ItensChecklist)
                    {
                        coluna.Item().Text(t =>
                        {
                            t.Span(item.Verificado ? "[X] " : "[ ] ").FontColor(CorMarca).Bold();
                            t.Span(item.Descricao);
                        });
                    }

                    coluna.Item().PaddingTop(8).Text("Participantes").FontSize(13).Bold();
                    foreach (var nome in modelo.ParticipantesNomes)
                    {
                        coluna.Item().Text($"• {nome}");
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

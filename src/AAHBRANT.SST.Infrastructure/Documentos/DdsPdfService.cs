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
                    CabecalhoDocumentoPadrao.Desenhar(coluna, "DDS — Diálogo Diário de Segurança", modelo.ObraNome, modelo.ObraLogoConteudo));

                pagina.Content().PaddingVertical(12).Column(coluna =>
                {
                    coluna.Spacing(8);

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

                    if (modelo.Temas.Count > 0)
                    {
                        coluna.Item().PaddingTop(8).Text("Temas do dia").FontSize(13).Bold();
                        foreach (var tema in modelo.Temas)
                        {
                            coluna.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(bloco =>
                            {
                                bloco.Spacing(2);
                                bloco.Item().Text(tema.AtividadeNome).Bold().FontColor(CorMarca);
                                if (tema.PerigoNome is null)
                                {
                                    bloco.Item().Text("Nenhum risco cadastrado para esta atividade — revisar Matriz de Riscos.");
                                }
                                else
                                {
                                    bloco.Item().Text(t => { t.Span("Perigo: ").SemiBold(); t.Span(tema.PerigoNome); });
                                    if (!string.IsNullOrWhiteSpace(tema.PerigoDescricao))
                                        bloco.Item().Text(t => { t.Span("Descrição: ").SemiBold(); t.Span(tema.PerigoDescricao); });
                                    if (!string.IsNullOrWhiteSpace(tema.Consequencia))
                                        bloco.Item().Text(t => { t.Span("Consequência: ").SemiBold(); t.Span(tema.Consequencia); });
                                    if (!string.IsNullOrWhiteSpace(tema.ControlesExistentes))
                                        bloco.Item().Text(t => { t.Span("Controles existentes: ").SemiBold(); t.Span(tema.ControlesExistentes); });
                                    if (!string.IsNullOrWhiteSpace(tema.ControlesAdicionais))
                                        bloco.Item().Text(t => { t.Span("Controles adicionais: ").SemiBold(); t.Span(tema.ControlesAdicionais); });
                                }
                            });
                        }
                    }

                    if (modelo.TemaLivreNome is not null)
                    {
                        coluna.Item().PaddingTop(4).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(bloco =>
                        {
                            bloco.Item().Text(t => { t.Span("Tema livre: ").SemiBold().FontColor(CorMarca); t.Span(modelo.TemaLivreNome); });
                            if (!string.IsNullOrWhiteSpace(modelo.TemaLivreDescricao))
                                bloco.Item().Text(modelo.TemaLivreDescricao);
                        });
                    }

                    // Pedido do usuário (30/09): o documento do DDS traz só os temas e a lista de
                    // presença — o checklist de verificação continua no sistema, mas não sai no PDF.
                    coluna.Item().PaddingTop(8).Text("Lista de Presença").FontSize(13).Bold();
                    coluna.Item().Table(tabela =>
                    {
                        tabela.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(36);
                            c.RelativeColumn();
                        });

                        tabela.Header(h =>
                        {
                            h.Cell().Background(CorMarca).Padding(4).AlignCenter().Text("Nº").FontColor(Colors.White).SemiBold();
                            h.Cell().Background(CorMarca).Padding(4).Text("Nome").FontColor(Colors.White).SemiBold();
                        });

                        var numero = 1;
                        foreach (var nome in modelo.ParticipantesNomes)
                        {
                            tabela.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignCenter().Text($"{numero++}");
                            tabela.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(nome);
                        }
                    });
                });

                pagina.Footer().Column(coluna => RodapeDocumentoPadrao.Desenhar(
                    coluna, "DDS", modelo.Protocolo, null, modelo.ConteudoHash, modelo.UrlValidacaoPublica, modelo.QrCodePng, modelo.TemAssinatura));
            });
        });

        return documento.GeneratePdf();
    }
}

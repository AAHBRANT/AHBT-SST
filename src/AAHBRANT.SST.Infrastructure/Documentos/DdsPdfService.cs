using AAHBRANT.SST.Application.Assinatura;
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
                    // Pedido do usuário (02/10): a assinatura fica na MESMA linha do nome, numa coluna
                    // própria — "Assinado digitalmente" e, embaixo em letra miúda, data/hora (Brasília;
                    // AssinadoEm é UTC) e o método de identificação (biometria facial/digital).
                    // Só entra na lista quem assinou (pedido de 02/10): com 10 funcionários e 8
                    // assinaturas, saem os 8 — quem não assinou não aparece no documento.
                    var assinantes = modelo.Participantes.Where(p => p.AssinadoEm is not null).ToList();
                    if (assinantes.Count == 0)
                    {
                        coluna.Item().Text("Nenhuma presença assinada até o momento.").Italic().FontColor(Colors.Grey.Darken1);
                    }
                    else coluna.Item().Table(tabela =>
                    {
                        tabela.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(36);
                            c.RelativeColumn(1.2f);
                            c.RelativeColumn(1.3f);
                        });

                        tabela.Header(h =>
                        {
                            h.Cell().Background(CorMarca).Padding(4).AlignCenter().Text("Nº").FontColor(Colors.White).SemiBold();
                            h.Cell().Background(CorMarca).Padding(4).Text("Nome").FontColor(Colors.White).SemiBold();
                            h.Cell().Background(CorMarca).Padding(4).AlignCenter().Text("Assinatura").FontColor(Colors.White).SemiBold();
                        });

                        var numero = 1;
                        foreach (var participante in assinantes)
                        {
                            tabela.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignCenter().AlignMiddle().Text($"{numero++}");
                            tabela.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignMiddle().Text(participante.Nome);
                            tabela.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignMiddle().Column(assinatura =>
                            {
                                if (participante.AssinadoEm is { } quando)
                                    AssinaturaPdfPadrao.Legenda(assinatura, quando, participante.Metodo);
                            });
                        }
                    });

                    // Pedido do usuário (05/10): as 3 fotos de evidência do encerramento saem no documento,
                    // lado a lado e do mesmo tamanho (a ordem 1–3 não tem significado), depois da lista de
                    // presença e antes da assinatura do responsável. ShowEntire evita partir o bloco.
                    if (modelo.Fotos is { Count: > 0 } fotos)
                    {
                        coluna.Item().PaddingTop(8).ShowEntire().Column(secao =>
                        {
                            secao.Spacing(6);
                            secao.Item().Text("Registro Fotográfico").FontSize(13).Bold();
                            secao.Item().Row(linha =>
                            {
                                linha.Spacing(8);
                                for (var i = 0; i < 3; i++)
                                {
                                    if (i >= fotos.Count) { linha.RelativeItem(); continue; }
                                    var foto = fotos[i];
                                    var numero = i + 1;
                                    linha.RelativeItem().Column(item =>
                                    {
                                        item.Spacing(3);
                                        item.Item().Height(105).Background(Colors.Grey.Lighten3).Border(1).BorderColor(Colors.Grey.Lighten2)
                                            .Padding(1).AlignCenter().AlignMiddle().Image(foto.Conteudo).FitArea();
                                        item.Item().Text(t =>
                                        {
                                            t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken2));
                                            t.Span($"Foto {numero}").Bold().FontColor(CorMarca);
                                            if (foto.CapturadaEm is { } quando)
                                                t.Span($" · {HorarioBrasilia.De(quando.UtcDateTime):dd/MM/yyyy HH:mm}");
                                            if (!string.IsNullOrWhiteSpace(foto.Local))
                                            {
                                                t.Line("");
                                                t.Span(foto.Local);
                                            }
                                            t.Line("");
                                            t.Span(foto.Latitude is { } lat && foto.Longitude is { } lon
                                                ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Lat {lat:0.0000} · Lon {lon:0.0000}")
                                                : "Sem geolocalização");
                                        });
                                    });
                                }
                            });
                        });
                    }

                    // Responsável/técnico pelo DDS (regra de 02/10): nome completo, linha e a
                    // assinatura miúda logo abaixo — mesmo padrão de todo documento do sistema.
                    coluna.Item().PaddingTop(24).AlignCenter().Width(260).Element(c => AssinaturaPdfPadrao.Bloco(
                        c, "Responsável pelo DDS", modelo.ResponsavelNome, modelo.ResponsavelFuncao, modelo.ResponsavelAssinadoEm, modelo.ResponsavelMetodo));
                });

                pagina.Footer().Column(coluna => RodapeDocumentoPadrao.Desenhar(
                    coluna, "DDS", modelo.Protocolo, null, modelo.ConteudoHash, modelo.UrlValidacaoPublica, modelo.QrCodePng, modelo.TemAssinatura));
            });
        });

        return documento.GeneratePdf();
    }
}

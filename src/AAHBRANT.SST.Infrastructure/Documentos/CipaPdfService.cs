using AAHBRANT.SST.Application.Cipa;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AAHBRANT.SST.Infrastructure.Documentos;

public class CipaPdfService : ICipaPdfService
{
    private const string CorMarca = "#670000";

    public byte[] GerarAtaEleicao(AtaEleicaoCipaPdfModelo modelo)
    {
        var documento = Document.Create(container =>
        {
            container.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(2, Unit.Centimetre);
                pagina.DefaultTextStyle(estilo => estilo.FontSize(10));

                pagina.Header().Column(coluna =>
                    CabecalhoDocumentoPadrao.Desenhar(coluna, "Ata de Eleição da CIPA", modelo.ObraNome, modelo.ObraLogoConteudo));

                pagina.Content().PaddingVertical(12).Column(coluna =>
                {
                    coluna.Spacing(8);

                    if (modelo.NumeroDocumento is not null)
                        coluna.Item().Text(t =>
                        {
                            t.Span("Nº do documento: ").SemiBold();
                            t.Span(modelo.NumeroDocumento);
                        });

                    coluna.Item().Row(linha =>
                    {
                        linha.RelativeItem().Text(t =>
                        {
                            t.Span("Convocação: ").SemiBold();
                            t.Span(modelo.DataConvocacao.ToString("dd/MM/yyyy"));
                        });
                        linha.RelativeItem().Text(t =>
                        {
                            t.Span("Votação: ").SemiBold();
                            t.Span(modelo.DataVotacao.ToString("dd/MM/yyyy"));
                        });
                        linha.RelativeItem().Text(t =>
                        {
                            t.Span("Apuração: ").SemiBold();
                            t.Span(modelo.DataApuracao?.ToString("dd/MM/yyyy HH:mm") ?? "—");
                        });
                    });

                    coluna.Item().PaddingTop(8).Text("Resultado da apuração").FontSize(13).Bold();

                    coluna.Item().Table(tabela =>
                    {
                        tabela.ColumnsDefinition(colunas =>
                        {
                            colunas.RelativeColumn(3);
                            colunas.RelativeColumn(1.5f);
                            colunas.ConstantColumn(50);
                            colunas.RelativeColumn(2);
                        });

                        tabela.Header(cabecalho =>
                        {
                            void Cab(string texto) => cabecalho.Cell().Background(CorMarca).Padding(4)
                                .Text(texto).FontSize(9).Bold().FontColor(Colors.White);
                            Cab("Candidato");
                            Cab("Matrícula");
                            Cab("Votos");
                            Cab("Resultado");
                        });

                        foreach (var candidato in modelo.Candidatos)
                        {
                            tabela.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text(candidato.Nome).FontSize(9);
                            tabela.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text(candidato.Matricula).FontSize(9);
                            tabela.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).AlignCenter().Text(candidato.Votos.ToString()).FontSize(9);
                            tabela.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text(candidato.StatusLabel).FontSize(9);
                        }
                    });

                    coluna.Item().PaddingTop(24).Row(linha =>
                    {
                        linha.RelativeItem().Column(c =>
                        {
                            c.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                            c.Item().AlignCenter().Text("Responsável pela apuração").FontSize(8);
                        });
                    });
                });

                pagina.Footer().Column(coluna => RodapeDocumentoPadrao.Desenhar(
                    coluna, "Ata de Eleição CIPA", modelo.NumeroDocumento, null, modelo.ConteudoHash, modelo.UrlValidacaoPublica, modelo.QrCodePng, temAssinatura: false));
            });
        });

        return documento.GeneratePdf();
    }

    public byte[] GerarAtaReuniao(AtaReuniaoCipaPdfModelo modelo)
    {
        var documento = Document.Create(container =>
        {
            container.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(2, Unit.Centimetre);
                pagina.DefaultTextStyle(estilo => estilo.FontSize(10));

                pagina.Header().Column(coluna =>
                    CabecalhoDocumentoPadrao.Desenhar(coluna, $"Ata de {modelo.TipoLabel} da CIPA", modelo.ObraNome, modelo.ObraLogoConteudo));

                pagina.Content().PaddingVertical(12).Column(coluna =>
                {
                    coluna.Spacing(8);

                    coluna.Item().Text(t =>
                    {
                        t.Span("Data: ").SemiBold();
                        t.Span(modelo.DataReuniao.ToString("dd/MM/yyyy"));
                    });

                    coluna.Item().Text("Pauta").FontSize(12).Bold();
                    coluna.Item().Text(modelo.Pauta ?? "—");

                    coluna.Item().PaddingTop(6).Text("Deliberações").FontSize(12).Bold();
                    coluna.Item().Text(modelo.Deliberacoes ?? "—");

                    coluna.Item().PaddingTop(8).Text("Lista de presença").FontSize(12).Bold();
                    foreach (var participante in modelo.Participantes)
                    {
                        coluna.Item().Text(t =>
                        {
                            t.Span(participante.Presente ? "[X] " : "[ ] ").FontColor(CorMarca).Bold();
                            t.Span(participante.Nome);
                        });
                    }
                });

                pagina.Footer().Column(coluna => RodapeDocumentoPadrao.Desenhar(
                    coluna, "Ata de Reunião CIPA", protocolo: null, null, modelo.ConteudoHash, modelo.UrlValidacaoPublica, modelo.QrCodePng, temAssinatura: false));
            });
        });

        return documento.GeneratePdf();
    }
}

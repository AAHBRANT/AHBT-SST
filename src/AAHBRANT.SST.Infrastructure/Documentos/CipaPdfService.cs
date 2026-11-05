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

                    coluna.Item().Element(c => GradeDocumentoPadrao.Desenhar(c, new GradeDocumentoPadrao.Campo[]
                    {
                        new("Nº do documento", modelo.NumeroDocumento, 1),
                        new("Convocação", modelo.DataConvocacao.ToString("dd/MM/yyyy"), 1),
                        new("Votação", modelo.DataVotacao.ToString("dd/MM/yyyy"), 1),
                        new("Apuração", modelo.DataApuracao?.ToString("dd/MM/yyyy HH:mm"), 1),
                    }));

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

                    coluna.Item().Element(c => GradeDocumentoPadrao.Desenhar(c, new GradeDocumentoPadrao.Campo[]
                    {
                        new("Data da reunião", modelo.DataReuniao.ToString("dd/MM/yyyy"), 1),
                        new("Tipo", modelo.TipoLabel, 3),
                        new("Pauta", modelo.Pauta, 4),
                        new("Deliberações", modelo.Deliberacoes, 4),
                    }));

                    coluna.Item().PaddingTop(8).Text("Lista de presença").FontSize(12).Bold();
                    coluna.Item().Table(tabela =>
                    {
                        tabela.ColumnsDefinition(c => { c.ConstantColumn(36); c.RelativeColumn(3); c.RelativeColumn(1); });
                        tabela.Header(h =>
                        {
                            h.Cell().Background(CorMarca).Padding(4).AlignCenter().Text("Nº").FontColor(Colors.White).SemiBold();
                            h.Cell().Background(CorMarca).Padding(4).Text("Participante").FontColor(Colors.White).SemiBold();
                            h.Cell().Background(CorMarca).Padding(4).AlignCenter().Text("Presença").FontColor(Colors.White).SemiBold();
                        });
                        var numero = 1;
                        foreach (var participante in modelo.Participantes)
                        {
                            tabela.Cell().Border(0.5f).BorderColor(GradeDocumentoPadrao.CorLinha).Padding(4).AlignCenter().Text($"{numero++}");
                            tabela.Cell().Border(0.5f).BorderColor(GradeDocumentoPadrao.CorLinha).Padding(4).Text(participante.Nome);
                            tabela.Cell().Border(0.5f).BorderColor(GradeDocumentoPadrao.CorLinha).Padding(4).AlignCenter()
                                .Text(participante.Presente ? "Presente" : "Ausente").FontColor(participante.Presente ? CorMarca : Colors.Grey.Darken1);
                        }
                    });
                });

                pagina.Footer().Column(coluna => RodapeDocumentoPadrao.Desenhar(
                    coluna, "Ata de Reunião CIPA", protocolo: null, null, modelo.ConteudoHash, modelo.UrlValidacaoPublica, modelo.QrCodePng, temAssinatura: false));
            });
        });

        return documento.GeneratePdf();
    }
}

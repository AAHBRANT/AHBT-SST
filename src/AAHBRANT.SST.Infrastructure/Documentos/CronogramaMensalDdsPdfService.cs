using System.Globalization;
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Dds.Cronogramas;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AAHBRANT.SST.Infrastructure.Documentos;

public class CronogramaMensalDdsPdfService : ICronogramaMensalDdsPdfService
{
    private const string Cor = "#670000";
    public byte[] Gerar(CronogramaDdsPdfModelo modelo, int? revisao = null, RastreabilidadeDocumentoResultado? rastreio = null)
    {
        if (modelo.Logo.Length == 0) throw new InvalidOperationException("A logo da obra é obrigatória.");
        var inicio = new DateOnly(modelo.Ano, modelo.Mes, 1);
        var quantidade = DateTime.DaysInMonth(modelo.Ano, modelo.Mes);
        var colunas = modelo.SemanaCompleta ? 7 : 5;
        var nomes = new[] { "SEGUNDA-FEIRA", "TERÇA-FEIRA", "QUARTA-FEIRA", "QUINTA-FEIRA", "SEXTA-FEIRA", "SÁBADO", "DOMINGO" };
        var dias = modelo.Dias.ToDictionary(d => d.Data);
        return Document.Create(documento => documento.Page(pagina =>
        {
            pagina.Size(PageSizes.A4.Landscape());
            pagina.Margin(1.2f, Unit.Centimetre);
            pagina.DefaultTextStyle(s => s.FontSize(9));
            pagina.Header().Column(c => CabecalhoDocumentoPadrao.Desenhar(c,
                "Cronograma Mensal de Temas de DDS", modelo.ObraNome, modelo.Logo));
            pagina.Content().PaddingVertical(10).Column(c =>
            {
                c.Spacing(8);
                c.Item().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(6).Row(r =>
                {
                    r.RelativeItem().Text($"Cliente / Fiscalização: {modelo.Cliente ?? "Não informado"}").FontSize(8);
                    r.RelativeItem().Text($"Competência: {inicio.ToString("MMMM/yyyy", CultureInfo.GetCultureInfo("pt-BR"))}").FontSize(8);
                    r.RelativeItem().Text($"Elaborado por: {modelo.Responsavel}").FontSize(8);
                });
                if (revisao is null)
                    c.Item().Text("PRÉVIA — programação ainda não emitida").FontSize(8).FontColor(Cor);
                c.Item().Table(t =>
                {
                    t.ColumnsDefinition(def => { for (var i = 0; i < colunas; i++) def.RelativeColumn(); });
                    t.Header(h =>
                    {
                        foreach (var nome in nomes.Take(colunas))
                            h.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1)
                                .Background("#F5EEEE").Padding(5).AlignCenter().Text(nome).FontSize(8).Bold().FontColor(Cor);
                    });
                    var segunda = inicio.AddDays(-((int)inicio.DayOfWeek + 6) % 7);
                    var fim = inicio.AddDays(quantidade - 1);
                    for (var semana = segunda; semana <= fim; semana = semana.AddDays(7))
                    {
                        if (!Enumerable.Range(0, colunas).Any(i => semana.AddDays(i).Month == modelo.Mes)) continue;
                        for (var i = 0; i < colunas; i++)
                        {
                            var data = semana.AddDays(i);
                            var celula = t.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1);
                            if (data.Month != modelo.Mes) { celula.Background("#FAFAFA").Text(""); continue; }
                            celula.Column(d =>
                            {
                                d.Item().Background("#FAFAFA").Padding(4).Text(data.ToString("dd/MM")).FontSize(8).Bold().FontColor(Cor);
                                d.Item().Padding(4).MinHeight(20).Column(temas =>
                                {
                                    if (dias.TryGetValue(data, out var dia) && dia.Temas.Count > 0)
                                    {
                                        foreach (var (tema, indice) in dia.Temas.Select((tema, indice) => (tema, indice)))
                                            temas.Item().PaddingBottom(3).Text((dia.Temas.Count > 1 ? $"{indice + 1}. " : "") + tema.Nome).FontSize(8);
                                    }
                                    else temas.Item().Text(data.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? "Sem programação" : "A definir").FontSize(8).FontColor(Colors.Grey.Medium);
                                });
                            });
                        }
                    }
                });
                c.Item().Text("Programação preliminar sujeita a alterações conforme as atividades, condições e necessidades da obra. " +
                    "Este documento apresenta os temas previstos e não comprova a realização dos DDS.").FontSize(8).Italic();
            });
            pagina.Footer().Column(c =>
            {
                if (rastreio is not null)
                    RodapeDocumentoPadrao.Desenhar(c, "Cronograma Mensal de DDS", modelo.NumeroDocumento, revisao,
                        rastreio.ConteudoHash, rastreio.UrlValidacaoPublica, rastreio.QrCodePng, false);
                else c.Item().AlignCenter().Text(t =>
                {
                    t.Span($"{modelo.NumeroDocumento} | Prévia | Página ").FontSize(8);
                    t.CurrentPageNumber().FontSize(8); t.Span(" de ").FontSize(8); t.TotalPages().FontSize(8);
                });
            });
        })).GeneratePdf();
    }
}

using AAHBRANT.SST.Application.Relatorios;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using static AAHBRANT.SST.Infrastructure.Documentos.RelatorioImagemEstilo;

namespace AAHBRANT.SST.Infrastructure.Documentos;

// Imagem da lista de presença do DDS (Telegram e página Relatórios): cards de presentes/ausentes/duração,
// ausentes com falta repetida, presentes com a hora da assinatura em duas colunas (os primeiros; o resto
// vai no PDF) e o horário de fechamento. Nomes completos, por decisão do usuário (07/10).
public class ImagemListaPresencaService : IImagemListaPresencaService
{
    private const int MaximoDePresentesNaImagem = 16;
    private const int MaximoDeAusentesNaImagem = 12;

    private static readonly string[] DiasDaSemana = { "domingo", "segunda", "terça", "quarta", "quinta", "sexta", "sábado" };

    public byte[] Gerar(ListaPresencaDados d)
    {
        return RenderizarPng(pagina =>
        {
            var dia = DiasDaSemana[(int)d.DataDds.DayOfWeek];
            var contexto = $"{d.Obra}  ·  {dia}, {d.DataDds:dd/MM/yyyy}" + (string.IsNullOrWhiteSpace(d.Tema) ? "" : $"  ·  {d.Tema}");
            pagina.Header().Element(c => Cabecalho(c, "SST  ·  LISTA DE PRESENÇA", "Presença do DDS", contexto));
            pagina.Content().Padding(24).Column(coluna =>
            {
                coluna.Spacing(14);
                coluna.Item().Element(c => Cards(c, d));
                if (d.PrimeiraAssinatura is not null) coluna.Item().Element(c => FaixaDeDuracao(c, d));
                if (d.Ausentes.Count > 0) coluna.Item().Element(c => Ausentes(c, d));
                if (d.Presentes.Count > 0) coluna.Item().Element(c => Presentes(c, d));
            });
            pagina.Footer().Element(c => Rodape(c, d.GeradoEm));
        });
    }

    private static void Cards(IContainer container, ListaPresencaDados d)
    {
        var percentual = d.Total > 0 ? (int)Math.Round(100.0 * d.QuantidadePresentes / d.Total) : 0;
        container.Row(linha =>
        {
            linha.Spacing(10);
            linha.RelativeItem(1.3f).Background(Vinho).CornerRadius(8).Padding(14).Column(c =>
            {
                c.Item().Text(t =>
                {
                    t.Span($"{d.QuantidadePresentes}").FontSize(34).Bold().FontColor("#ffffff");
                    t.Span($" de {d.Total}").FontSize(14).SemiBold().FontColor("#e8d6d6");
                });
                c.Item().Text($"Presentes ({percentual}%)").FontSize(11).FontColor("#f1e4e4");
            });
            linha.RelativeItem().Border(1).BorderColor(Linha).Background("#ffffff").CornerRadius(8).Padding(14).Column(c =>
            {
                c.Item().Text($"{d.Ausentes.Count}").FontSize(34).Bold();
                c.Item().Text("Ausentes").FontSize(11).FontColor(Cinza);
            });
            linha.RelativeItem().Border(1).BorderColor(Linha).Background("#ffffff").CornerRadius(8).Padding(14).Column(c =>
            {
                if (d.DuracaoMinutos is { } minutos)
                    c.Item().Text(t =>
                    {
                        t.Span(minutos < 60 ? $"{minutos}" : $"{minutos / 60}h{minutos % 60:00}").FontSize(34).Bold();
                        if (minutos < 60) t.Span(" min").FontSize(14).SemiBold().FontColor(Cinza);
                    });
                else
                    c.Item().Text("—").FontSize(34).Bold().FontColor(Cinza);
                c.Item().Text(d.DuracaoMinutos is null ? "Duração indisponível" : "Duração do DDS").FontSize(11).FontColor(Cinza);
            });
        });
    }

    private static void FaixaDeDuracao(IContainer container, ListaPresencaDados d)
    {
        container.Column(c =>
        {
            c.Spacing(8);
            var texto = d.DuracaoMinutos is not null && d.UltimaAssinatura is not null
                ? $"1ª assinatura {d.PrimeiraAssinatura:HH:mm}, última {d.UltimaAssinatura:HH:mm}"
                : $"Só uma assinatura registrada ({d.PrimeiraAssinatura:HH:mm}): sem como calcular a duração";
            c.Item().Border(1).BorderColor(VerdeBorda).Background(VerdeFundo).CornerRadius(8).PaddingVertical(10).PaddingHorizontal(14)
                .Text(texto).FontSize(12).SemiBold().FontColor(Verde);
            if (d.FechadoEm is { } fechado)
            {
                var ate = d.MinutosAteFechar is { } m ? $" ({FormatarMinutos(m)} até o fechamento)" : "";
                c.Item().Border(1).BorderColor(AtencaoBorda).Background(AtencaoFundo).CornerRadius(8).PaddingVertical(10).PaddingHorizontal(14)
                    .Text($"Fechado às {fechado:HH:mm}{ate}").FontSize(12).SemiBold().FontColor(AtencaoTexto);
            }
        });
    }

    private static string FormatarMinutos(int m) => m < 60 ? $"{m} min" : $"{m / 60}h{m % 60:00}";

    private static void Ausentes(IContainer container, ListaPresencaDados d)
    {
        var exibidos = d.Ausentes.Take(MaximoDeAusentesNaImagem).ToList();
        container.Column(c =>
        {
            c.Item().PaddingBottom(6).Text("AUSENTES").FontSize(10).Bold().FontColor(Vinho).LetterSpacing(0.08f);
            c.Item().Border(1).BorderColor(AlertaBorda).Background(AlertaFundo).CornerRadius(8).Padding(14).Column(lista =>
            {
                lista.Spacing(6);
                foreach (var a in exibidos)
                {
                    lista.Item().Row(linha =>
                    {
                        linha.RelativeItem().Text(a.Nome).FontSize(12).FontColor(Vinho);
                        var detalhe = (string.IsNullOrWhiteSpace(a.Matricula) ? "" : $"Mat. {a.Matricula}")
                            + (a.FaltasSeguidas >= 2 ? $"{(string.IsNullOrWhiteSpace(a.Matricula) ? "" : " · ")}{a.FaltasSeguidas}º DDS seguido" : "");
                        linha.AutoItem().Text(detalhe).FontSize(11).FontColor(Vinho);
                    });
                }
                if (d.Ausentes.Count > exibidos.Count)
                    lista.Item().Text($"e mais {d.Ausentes.Count - exibidos.Count} no PDF").FontSize(10).FontColor(Vinho);
            });
        });
    }

    private static void Presentes(IContainer container, ListaPresencaDados d)
    {
        var exibidos = d.Presentes.Take(MaximoDePresentesNaImagem).ToList();
        container.Column(c =>
        {
            c.Item().PaddingBottom(6).Text("PRESENTES E HORA DA ASSINATURA").FontSize(10).Bold().FontColor(Vinho).LetterSpacing(0.08f);
            c.Item().Border(1).BorderColor(Linha).Background("#ffffff").CornerRadius(8).Padding(14).Column(lista =>
            {
                lista.Spacing(5);
                for (var i = 0; i < exibidos.Count; i += 2)
                {
                    var esquerda = exibidos[i];
                    var direita = i + 1 < exibidos.Count ? exibidos[i + 1] : null;
                    lista.Item().Row(linha =>
                    {
                        linha.Spacing(16);
                        linha.RelativeItem().Element(x => Pessoa(x, esquerda));
                        if (direita is not null) linha.RelativeItem().Element(x => Pessoa(x, direita));
                        else linha.RelativeItem();
                    });
                }
                if (d.Presentes.Count > exibidos.Count)
                    lista.Item().Text($"e mais {d.Presentes.Count - exibidos.Count} no PDF").FontSize(10).FontColor(Cinza);
            });
        });
    }

    private static void Pessoa(IContainer container, PresencaLinha p)
    {
        container.Row(linha =>
        {
            linha.RelativeItem().Text(p.Nome).FontSize(10.5f).ClampLines(1);
            linha.AutoItem().PaddingLeft(6).Text(p.AssinadoEm is { } quando ? $"{quando:HH:mm}" : "—").FontSize(10.5f).Bold().FontColor(Vinho);
        });
    }
}

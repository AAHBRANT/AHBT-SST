using AAHBRANT.SST.Application.Dds;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static AAHBRANT.SST.Infrastructure.Documentos.RelatorioImagemEstilo;

namespace AAHBRANT.SST.Infrastructure.Documentos;

// Relatório do DDS como IMAGEM para o Telegram (07/10): cabeçalho vinho AAHBRANT, cards de presença,
// barra por método, alerta de falhas do facial e cadastros a revisar. legível no
// celular, com 1080 px de largura e altura que se ajusta ao conteúdo. Reaproveita o QuestPDF (renderiza a
// página como PNG). Só matrícula e quantidades.
public class ImagemResumoDdsService : IImagemResumoDdsService
{
    private const string CorFacial = "#670000";
    private const string CorDigital = "#b88a6a";
    private const string CorPendente = "#cfc9c0";

    private const int MaximoDeMatriculasNaImagem = 6;

    public byte[] Gerar(ResumoDdsDados d)
    {
        return RenderizarPng(pagina =>
        {
            pagina.Header().Element(c => Cabecalho(c, "SST  ·  Relatório do DDS", "DDS encerrado", $"{d.Obra}  ·  {d.Quando:dd/MM/yyyy, HH:mm}"));
            pagina.Content().Padding(24).Column(coluna =>
            {
                coluna.Spacing(14);
                coluna.Item().Element(c => Cards(c, d));
                coluna.Item().Element(c => BarraDePresenca(c, d));
                coluna.Item().Element(c => AlertaDeFalhas(c, d));
                coluna.Item().Element(c => Revisao(c, d));
            });
            pagina.Footer().Element(c => Rodape(c, d.Quando));
        });
    }

    private static void Cards(IContainer container, ResumoDdsDados d)
    {
        var percentual = d.Total > 0 ? (int)Math.Round(100.0 * d.Presencas / d.Total) : 0;
        container.Row(linha =>
        {
            linha.Spacing(10);
            linha.RelativeItem(1.3f).Background(Vinho).CornerRadius(8).Padding(14).Column(c =>
            {
                c.Item().Text(t =>
                {
                    t.Span($"{d.Presencas}").FontSize(34).Bold().FontColor("#ffffff");
                    t.Span($" de {d.Total}").FontSize(14).SemiBold().FontColor("#e8d6d6");
                });
                c.Item().Text($"Presenças ({percentual}%)").FontSize(11).FontColor("#f1e4e4");
            });
            linha.RelativeItem().Border(1).BorderColor(Linha).Background("#ffffff").CornerRadius(8).Padding(14).Column(c =>
            {
                c.Item().Text($"{d.Facial}").FontSize(34).Bold();
                c.Item().Text("Facial").FontSize(11).FontColor(Cinza);
            });
            linha.RelativeItem().Border(1).BorderColor(Linha).Background("#ffffff").CornerRadius(8).Padding(14).Column(c =>
            {
                c.Item().Text($"{d.Digital}").FontSize(34).Bold();
                c.Item().Text("Digital").FontSize(11).FontColor(Cinza);
            });
        });
    }

    private static void BarraDePresenca(IContainer container, ResumoDdsDados d)
    {
        var total = Math.Max(1, d.Facial + d.Digital + d.Pendentes);
        container.Column(c =>
        {
            c.Item().Height(16).CornerRadius(6).Row(linha =>
            {
                if (d.Facial > 0) linha.RelativeItem(d.Facial).Background(CorFacial);
                if (d.Digital > 0) linha.RelativeItem(d.Digital).Background(CorDigital);
                if (d.Pendentes > 0) linha.RelativeItem(d.Pendentes).Background(CorPendente);
                if (d.Facial + d.Digital + d.Pendentes == 0) linha.RelativeItem(total).Background(CorPendente);
            });
            c.Item().PaddingTop(6).Text(t =>
            {
                t.DefaultTextStyle(e => e.FontSize(10).FontColor(Cinza));
                t.Span("■ ").FontColor(CorFacial); t.Span($"Facial {d.Facial}      ");
                t.Span("■ ").FontColor(CorDigital); t.Span($"Digital {d.Digital}      ");
                t.Span("■ ").FontColor(CorPendente); t.Span($"Pendentes {d.Pendentes}");
            });
        });
    }

    private static void AlertaDeFalhas(IContainer container, ResumoDdsDados d)
    {
        // Sem falha não é alerta: o quadro fica verde, para um DDS sem problema não parecer problemático.
        var semFalhas = d.Falhas == 0;
        var cor = semFalhas ? "#1f6b3a" : Vinho;
        container.Border(1).BorderColor(semFalhas ? "#c5dfcb" : "#e2c9c9").Background(semFalhas ? "#eaf4ec" : "#fbeeee")
            .CornerRadius(8).PaddingVertical(12).PaddingHorizontal(14).Row(linha =>
        {
            linha.AutoItem().AlignMiddle().Text($"{d.Falhas}").FontSize(24).Bold().FontColor(cor);
            linha.RelativeItem().PaddingLeft(12).AlignMiddle()
                .Text(semFalhas ? "falhas do reconhecimento facial neste DDS"
                    : d.Falhas == 1 ? "falha do reconhecimento facial neste DDS" : "falhas do reconhecimento facial neste DDS")
                .FontSize(12).FontColor(cor);
        });
    }

    private static void Revisao(IContainer container, ResumoDdsDados d)
    {
        if (d.Fracos.Count == 0)
        {
            container.Background(Bege).CornerRadius(8).Padding(16).AlignCenter()
                .Text("Nenhum cadastro facial para revisar.").FontSize(12).FontColor("#3d3b10");
            return;
        }

        var exibidos = d.Fracos.OrderByDescending(f => f.Falhas).Take(MaximoDeMatriculasNaImagem).ToList();
        var maior = Math.Max(1, exibidos.Max(f => f.Falhas));
        var restantes = d.Fracos.Count - exibidos.Count;

        container.Column(c =>
        {
            c.Item().PaddingBottom(6).Text("REVISAR CADASTRO FACIAL").FontSize(10).Bold().FontColor(Vinho).LetterSpacing(0.08f);
            c.Item().Background(Bege).CornerRadius(8).Padding(14).Column(lista =>
            {
                lista.Spacing(9);
                foreach (var f in exibidos)
                {
                    var preenchido = Math.Max(0.05f, (float)f.Falhas / maior);
                    lista.Item().Row(linha =>
                    {
                        linha.ConstantItem(78).AlignMiddle().Text(string.IsNullOrWhiteSpace(f.Matricula) ? "s/ matrícula" : $"Mat. {f.Matricula}").FontSize(12).FontColor("#3d3b10");
                        linha.RelativeItem().AlignMiddle().Height(8).Row(barra =>
                        {
                            barra.RelativeItem(preenchido).Background(Vinho).CornerRadius(3);
                            if (preenchido < 1f) barra.RelativeItem(1f - preenchido);
                        });
                        linha.ConstantItem(34).AlignMiddle().AlignRight().Text($"{f.Falhas}").FontSize(13).Bold().FontColor(Vinho);
                    });
                }
                if (restantes > 0)
                    lista.Item().Text($"e mais {restantes}").FontSize(10).FontColor("#3d3b10");
            });
        });
    }
}

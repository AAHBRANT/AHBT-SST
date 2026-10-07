using AAHBRANT.SST.Application.Dds;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AAHBRANT.SST.Infrastructure.Documentos;

// Relatório do DDS como IMAGEM para o Telegram (07/10): cabeçalho vinho AAHBRANT, cards de presença,
// barra por método, alerta de falhas do facial e cadastros a revisar. legível no
// celular, com 1080 px de largura e altura que se ajusta ao conteúdo. Reaproveita o QuestPDF (renderiza a
// página como PNG). Só matrícula e quantidades.
public class ImagemResumoDdsService : IImagemResumoDdsService
{
    private const string Vinho = "#670000";
    private const string Bege = "#ebe9ad";
    private const string Linha = "#e2ddd7";
    private const string Fundo = "#fbfaf7";
    private const string Cinza = "#6b6560";
    private const string Tinta = "#1c1a19";
    private const string CorFacial = "#670000";
    private const string CorDigital = "#b88a6a";
    private const string CorPendente = "#cfc9c0";

    // Página em pontos (72 por polegada); 144 dpi de rasterização dá exatamente 1080 px de largura. A altura
    // é contínua (cresce com a lista de matrículas, limitada a MaximoDeMatriculasNaImagem).
    private const float LarguraPt = 540f;
    private const int Dpi = 144;
    private const int MaximoDeMatriculasNaImagem = 6;

    public byte[] Gerar(ResumoDdsDados d)
    {
        var documento = Document.Create(container =>
        {
            container.Page(pagina =>
            {
                pagina.ContinuousSize(LarguraPt);
                pagina.Margin(0);
                pagina.PageColor(Fundo);
                pagina.DefaultTextStyle(e => e.FontSize(12).FontColor(Tinta));

                pagina.Header().Element(c => Cabecalho(c, d));
                pagina.Content().Padding(24).Column(coluna =>
                {
                    coluna.Spacing(14);
                    coluna.Item().Element(c => Cards(c, d));
                    coluna.Item().Element(c => BarraDePresenca(c, d));
                    coluna.Item().Element(c => AlertaDeFalhas(c, d));
                    coluna.Item().Element(c => Revisao(c, d));
                });
                pagina.Footer().Element(c => Rodape(c, d));
            });
        });

        var imagens = documento.GenerateImages(new ImageGenerationSettings
        {
            ImageFormat = ImageFormat.Png,
            RasterDpi = Dpi,
        });
        return imagens.First();
    }

    // Logomarca oficial (PNG com fundo transparente, preta e vinho): embutida no assembly. Fica numa faixa
    // branca no topo porque não tem leitura sobre o fundo vinho.
    private const string RecursoLogo = "AAHBRANT.SST.Infrastructure.Documentos.Assets.logo-aahbrant.png";
    private static readonly Lazy<byte[]?> Logo = new(() =>
    {
        using var stream = typeof(ImagemResumoDdsService).Assembly.GetManifestResourceStream(RecursoLogo);
        if (stream is null) return null;
        using var memoria = new MemoryStream();
        stream.CopyTo(memoria);
        return memoria.ToArray();
    });

    private static void Cabecalho(IContainer container, ResumoDdsDados d)
    {
        container.Column(topo =>
        {
            topo.Item().Background("#ffffff").PaddingVertical(16).PaddingHorizontal(28).Row(linha =>
            {
                if (Logo.Value is { } logo)
                    linha.AutoItem().Height(40).Image(logo).FitHeight();
                else
                    linha.AutoItem().AlignMiddle().Text("AAHBRANT").FontSize(18).Bold().FontColor(Vinho);
                linha.RelativeItem().AlignMiddle().AlignRight().Text("SST  ·  Relatório do DDS").FontSize(9).FontColor(Cinza).SemiBold().LetterSpacing(0.12f);
            });
            topo.Item().Background(Vinho).PaddingVertical(22).PaddingHorizontal(28).Column(c =>
            {
                c.Item().Text("DDS encerrado").FontSize(30).Bold().FontColor("#ffffff");
                c.Item().PaddingTop(4).Text($"{d.Obra}  ·  {d.Quando:dd/MM/yyyy, HH:mm}").FontSize(12).FontColor("#f1e4e4");
            });
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

    private static void Rodape(IContainer container, ResumoDdsDados d)
    {
        container.BorderTop(1).BorderColor(Linha).PaddingVertical(10).PaddingHorizontal(28).Row(linha =>
        {
            linha.RelativeItem().Text("Sistema SST AAHBRANT").FontSize(9).FontColor(Cinza);
            linha.AutoItem().Text($"Gerado às {d.Quando:HH:mm}").FontSize(9).FontColor(Cinza);
        });
    }
}

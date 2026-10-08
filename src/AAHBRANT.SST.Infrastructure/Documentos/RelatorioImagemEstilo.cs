using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace AAHBRANT.SST.Infrastructure.Documentos;

// Estilo comum das imagens de relatório enviadas ao Telegram e guardadas no sistema (padrão AAHBRANT):
// faixa branca com a logomarca oficial, bloco de título em vinho, cores da marca e rodapé. Todos os
// relatórios usam isto para ficarem iguais.
public static class RelatorioImagemEstilo
{
    public const string Vinho = "#670000";
    public const string Bege = "#ebe9ad";
    public const string BegeTexto = "#3d3b10";
    public const string Linha = "#e2ddd7";
    public const string Fundo = "#fbfaf7";
    public const string Cinza = "#6b6560";
    public const string Tinta = "#1c1a19";
    public const string Verde = "#1f6b3a";
    public const string VerdeFundo = "#eaf4ec";
    public const string VerdeBorda = "#c5dfcb";
    public const string AlertaFundo = "#fbeeee";
    public const string AlertaBorda = "#e2c9c9";
    public const string AtencaoFundo = "#fdf3d9";
    public const string AtencaoBorda = "#ecd9a8";
    public const string AtencaoTexto = "#7a5200";

    // Página em pontos (72 por polegada); 144 dpi de rasterização dá 1080 px de largura. A altura é contínua.
    public const float LarguraPt = 540f;
    public const int Dpi = 144;

    // Logomarca oficial (PNG com fundo transparente, preta e vinho): embutida no assembly. Fica numa faixa
    // branca no topo porque não tem leitura sobre o fundo vinho.
    private const string RecursoLogo = "AAHBRANT.SST.Infrastructure.Documentos.Assets.logo-aahbrant.png";
    private static readonly Lazy<byte[]?> Logo = new(() =>
    {
        using var stream = typeof(RelatorioImagemEstilo).Assembly.GetManifestResourceStream(RecursoLogo);
        if (stream is null) return null;
        using var memoria = new MemoryStream();
        stream.CopyTo(memoria);
        return memoria.ToArray();
    });

    public static byte[] RenderizarPng(Action<PageDescriptor> pagina)
    {
        var documento = Document.Create(container => container.Page(p =>
        {
            p.ContinuousSize(LarguraPt);
            p.Margin(0);
            p.PageColor(Fundo);
            p.DefaultTextStyle(e => e.FontSize(12).FontColor(Tinta));
            pagina(p);
        }));
        return documento.GenerateImages(new ImageGenerationSettings { ImageFormat = ImageFormat.Png, RasterDpi = Dpi }).First();
    }

    // Faixa branca com a logo + bloco vinho com o título e a linha de contexto.
    public static void Cabecalho(IContainer container, string rotuloDaFaixa, string titulo, string contexto)
    {
        container.Column(topo =>
        {
            topo.Item().Background("#ffffff").PaddingVertical(16).PaddingHorizontal(28).Row(linha =>
            {
                if (Logo.Value is { } logo)
                    linha.AutoItem().Height(40).Image(logo).FitHeight();
                else
                    linha.AutoItem().AlignMiddle().Text("AAHBRANT").FontSize(18).Bold().FontColor(Vinho);
                linha.RelativeItem().AlignMiddle().AlignRight().Text(rotuloDaFaixa).FontSize(9).FontColor(Cinza).SemiBold().LetterSpacing(0.12f);
            });
            topo.Item().Background(Vinho).PaddingVertical(22).PaddingHorizontal(28).Column(c =>
            {
                c.Item().Text(titulo).FontSize(30).Bold().FontColor("#ffffff");
                c.Item().PaddingTop(4).Text(contexto).FontSize(12).FontColor("#f1e4e4");
            });
        });
    }

    public static void Rodape(IContainer container, DateTime geradoEm)
    {
        container.BorderTop(1).BorderColor(Linha).PaddingVertical(10).PaddingHorizontal(28).Row(linha =>
        {
            linha.RelativeItem().Text("Sistema SST AAHBRANT").FontSize(9).FontColor(Cinza);
            linha.AutoItem().Text($"Gerado às {geradoEm:HH:mm}").FontSize(9).FontColor(Cinza);
        });
    }
}

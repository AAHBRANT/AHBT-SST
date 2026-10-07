using AAHBRANT.SST.Application.Dds;
using AAHBRANT.SST.Infrastructure.Documentos;
using SixLabors.ImageSharp;

namespace AAHBRANT.SST.Application.Tests.Dds;

// A imagem do relatório do DDS (Telegram): PNG de 1080 px de largura no padrão AAHBRANT, uma única imagem
// com altura que se ajusta ao conteúdo (limites abaixo mantêm o formato bom para o Telegram).
public class ImagemResumoDdsServiceTests
{
    static ImagemResumoDdsServiceTests() => QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

    private static ResumoDdsDados Dados(int fracos = 2, int falhas = 11, int presencas = 28, int total = 30) => new(
        "Obra Sul", new DateTime(2026, 10, 7, 7, 12, 0), presencas, total, 19, 9, falhas,
        Enumerable.Range(0, fracos).Select(i => ((string?)$"{31 + i}", 7 - (i % 5))).ToList());

    private static byte[] Gerar(ResumoDdsDados d) => new ImagemResumoDdsService().Gerar(d);

    private static (int Largura, int Altura) Tamanho(byte[] png)
    {
        var info = Image.Identify(png);
        return (info.Width, info.Height);
    }

    [Fact]
    public void GeraPngDe1080DeLargura()
    {
        var png = Gerar(Dados());

        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png.Take(4).ToArray()); // assinatura PNG
        var (largura, altura) = Tamanho(png);
        Assert.Equal(1080, largura);
        Assert.InRange(altura, 900, 1600);
    }

    [Fact]
    public void SemCadastrosParaRevisar_AindaGera()
    {
        var png = Gerar(Dados(fracos: 0, falhas: 0));

        Assert.Equal(1080, Tamanho(png).Largura);
    }

    [Fact]
    public void ComMuitosCadastros_ContinuaEmUmaImagemSo()
    {
        // 30 matrículas: a imagem mostra as 6 maiores e "e mais 24"; a altura não cresce sem limite.
        var comMuitos = Tamanho(Gerar(Dados(fracos: 30)));
        var comPoucos = Tamanho(Gerar(Dados(fracos: 6)));

        Assert.Equal(comPoucos.Altura + 18, comMuitos.Altura, tolerance: 40); // só a linha "e mais N" a mais
    }

    [Fact]
    public void SemNinguemPresenteNemSelecionado_NaoQuebra()
    {
        var png = Gerar(new ResumoDdsDados("Obra", new DateTime(2026, 10, 7, 7, 0, 0), 0, 0, 0, 0, 0, Array.Empty<(string?, int)>()));

        Assert.Equal(1080, Image.Identify(png).Width);
    }

    [Fact]
    public void MatriculaVazia_NaoQuebra()
    {
        var png = Gerar(new ResumoDdsDados("Obra", new DateTime(2026, 10, 7, 7, 0, 0), 1, 2, 1, 0, 3, new[] { ((string?)null, 3) }));

        Assert.Equal(1080, Tamanho(png).Largura);
    }
}

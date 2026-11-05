using AAHBRANT.SST.Application.Trabalhadores;
using AAHBRANT.SST.Infrastructure.Documentos;
using AAHBRANT.SST.Infrastructure.Trabalhadores;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AAHBRANT.SST.Infrastructure.Tests.Documentos;

public class DossieTrabalhadorPdfTests
{
    private static byte[] FotoDeitada1280x720()
    {
        using var imagem = new Image<Rgb24>(1280, 720, new Rgb24(90, 120, 150));
        using var ms = new MemoryStream();
        imagem.SaveAsJpeg(ms);
        return ms.ToArray();
    }

    [Theory]
    [InlineData(78f, 96f)]  // foto de cadastro do relatório (em pé)
    [InlineData(54f, 54f)]  // foto da assinatura no cofre (quadrada)
    [InlineData(104f, 132f)] // biometria na Ficha de EPI
    public void Recortar_FotoDeitada_SaiNaProporcaoDoQuadro(float largura, float altura)
    {
        var recortada = FotoQuadroPdf.Recortar(FotoDeitada1280x720(), largura, altura);

        Assert.NotNull(recortada);
        var info = Image.Identify(recortada);
        Assert.Equal(largura / altura, info.Width / (double)info.Height, 2);
        Assert.Equal(720, info.Height); // corta só as laterais, sem perder altura
    }

    [Fact]
    public void Recortar_BytesQueNaoSaoImagem_DevolveNulo()
        => Assert.Null(FotoQuadroPdf.Recortar(new byte[] { 1, 2, 3, 4 }, 54, 54));

    [Fact]
    public void GerarIndice_ComItensIncluidosENaoIncluidos_GeraPdf()
    {
        var servico = new DossieTrabalhadorPdfService();
        var itens = new List<ItemIndiceDossie>
        {
            new("Certificados de treinamento", "Certificado — NR-35", new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc), true),
            new("ASO — Atestado de Saúde Ocupacional", "ASO Admissional", null, false, "Arquivo danificado."),
        };

        var pdf = servico.GerarIndice("Fulano de Tal", "Obra Teste", null, itens);

        Assert.True(pdf.Length > 0);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void ImagemComoPdf_FotoValida_GeraPdf_EArquivoInvalido_DevolveNulo()
    {
        var servico = new DossieTrabalhadorPdfService();

        var pdf = servico.ImagemComoPdf("ASO Admissional", FotoDeitada1280x720(), "Obra Teste", null);
        Assert.NotNull(pdf);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf!, 0, 4));

        Assert.Null(servico.ImagemComoPdf("ASO", new byte[] { 0x50, 0x4B, 3, 4 }, "Obra Teste", null));
    }
}

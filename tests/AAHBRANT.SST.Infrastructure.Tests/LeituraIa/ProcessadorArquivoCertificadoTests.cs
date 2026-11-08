using AAHBRANT.SST.Infrastructure.LeituraIa;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AAHBRANT.SST.Infrastructure.Tests.LeituraIa;

// Certificado endireitado (07/11/2026): o arquivo é guardado já girado.
public class ProcessadorArquivoCertificadoTests
{
    private static byte[] ImagemPaisagem(int largura = 40, int altura = 20)
    {
        using var img = new Image<Rgba32>(largura, altura);
        img[0, 0] = new Rgba32(255, 0, 0); // canto superior esquerdo vermelho
        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        return ms.ToArray();
    }

    [Fact]
    public void Girar90_TrocaLarguraEAltura()
    {
        var (conteudo, tipo) = new ProcessadorArquivoCertificado().Girar(ImagemPaisagem(), "image/png", 90);
        using var img = Image.Load<Rgba32>(conteudo);
        Assert.Equal("image/png", tipo);
        Assert.Equal(20, img.Width);
        Assert.Equal(40, img.Height);
    }

    [Fact]
    public void Girar180_LevaOCantoParaOLadoOposto()
    {
        var (conteudo, _) = new ProcessadorArquivoCertificado().Girar(ImagemPaisagem(), "image/png", 180);
        using var img = Image.Load<Rgba32>(conteudo);
        Assert.Equal(new Rgba32(255, 0, 0), img[39, 19]);
    }

    [Fact]
    public void Girar0_NaoMexeNoArquivo()
    {
        var original = ImagemPaisagem();
        var (conteudo, _) = new ProcessadorArquivoCertificado().Girar(original, "image/png", 0);
        Assert.Same(original, conteudo);
    }

    [Fact]
    public void ImagemParaLeitura_DeFoto_DevolveJpeg()
    {
        var jpeg = new ProcessadorArquivoCertificado().ImagemParaLeitura(ImagemPaisagem(), "image/png");
        Assert.NotNull(jpeg);
        Assert.Equal(0xFF, jpeg![0]);
        Assert.Equal(0xD8, jpeg[1]);
    }
}

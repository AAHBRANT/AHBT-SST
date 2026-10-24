using AAHBRANT.SST.Infrastructure.Imagens;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AAHBRANT.SST.Infrastructure.Tests.Imagens;

public class ImagemSeguraTests
{
    private static byte[] Gerar(int largura, int altura, Action<Image, MemoryStream> salvar)
    {
        using var imagem = new Image<Rgba32>(largura, altura);
        using var ms = new MemoryStream();
        salvar(imagem, ms);
        return ms.ToArray();
    }

    [Fact]
    public void Jpeg_e_Png_normais_sao_aceitos()
    {
        using var jpeg = ImagemSegura.Carregar(Gerar(1600, 1200, (i, s) => i.SaveAsJpeg(s)));
        using var png = ImagemSegura.Carregar(Gerar(800, 600, (i, s) => i.SaveAsPng(s)));

        Assert.Equal(1600, jpeg.Width);
        Assert.Equal(600, png.Height);
    }

    [Fact]
    public void Formato_fora_da_lista_e_recusado()
    {
        var bmp = Gerar(100, 100, (i, s) => i.SaveAsBmp(s));

        var erro = Assert.Throws<InvalidOperationException>(() => ImagemSegura.Carregar(bmp));
        Assert.Contains("Formato de imagem não suportado", erro.Message);
        Assert.False(ImagemSegura.EhValida(bmp));
    }

    [Fact]
    public void Imagem_acima_do_limite_e_recusada_antes_de_decodificar()
    {
        var grande = Gerar(ImagemSegura.LadoMaximoPx + 1, 10, (i, s) => i.SaveAsPng(s));

        var erro = Assert.Throws<InvalidOperationException>(() => ImagemSegura.Validar(grande));
        Assert.Contains("grande demais", erro.Message);
    }

    [Fact]
    public void Bytes_que_nao_sao_imagem_sao_recusados()
    {
        Assert.False(ImagemSegura.EhValida(new byte[] { 1, 2, 3 }));
    }
}

using System.Drawing;
using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

namespace AAHBRANT.SST.AgenteCameras.Quadros;

// Porte de src/AAHBRANT.SST.TeamsApp/src/pages/cameras/detectorMovimento.ts (mesmas constantes e mesma conta):
// quadros reduzidos a 64x36 em tons de cinza; a luminância média de cada quadro é subtraída antes de comparar
// (mudança global de brilho não conta); movimento = 2% ou mais dos pixels com diferença acima de 25.
public static class DetectorMovimento
{
    public const int LarguraAmostra = 64;
    public const int AlturaAmostra = 36;
    public const int LimiarPixel = 25;
    public const double LimiarFracao = 0.02;

    public static double FracaoAlterada(byte[] anterior, byte[] atual, int limiarPixel = LimiarPixel)
    {
        if (anterior.Length != atual.Length || atual.Length == 0) return 0;
        var mediaAnterior = Media(anterior);
        var mediaAtual = Media(atual);
        var alterados = 0;
        for (var i = 0; i < atual.Length; i++)
            if (Math.Abs(atual[i] - mediaAtual - (anterior[i] - mediaAnterior)) > limiarPixel) alterados++;
        return (double)alterados / atual.Length;
    }

    public static bool HouveMovimento(byte[]? anterior, byte[] atual)
    {
        if (anterior is null) return true; // primeiro quadro: referência
        return FracaoAlterada(anterior, atual) >= LimiarFracao;
    }

    private static double Media(byte[] v)
    {
        double soma = 0;
        foreach (var b in v) soma += b;
        return soma / v.Length;
    }
}

public interface IAmostrador
{
    /// <summary>JPEG -> amostra 64x36 em cinza (um byte por pixel). Null se o JPEG não puder ser decodificado.</summary>
    byte[]? Amostrar(byte[] jpeg);
}

// Decodifica e reduz com System.Drawing (GDI+). Escolha: o projeto é só Windows e o GDI+ já vem no sistema,
// sem pacote nativo extra nem licença de terceiros (alternativas como ImageSharp/SkiaSharp trazem dependência
// e, no caso do ImageSharp, licença comercial). 1 quadro por segundo a ~1280x720 custa poucos milissegundos.
// Diferença para o navegador: aqui a redução usa média de área (HighQualityBilinear), que é mais estável a
// ruído do sensor que a amostragem do canvas; a conta de cinza é a mesma (299/587/114, truncada).
[SupportedOSPlatform("windows")]
public sealed class AmostradorGdi : IAmostrador
{
    public byte[]? Amostrar(byte[] jpeg)
    {
        try
        {
            using var ms = new MemoryStream(jpeg);
            using var imagem = Image.FromStream(ms, useEmbeddedColorManagement: false, validateImageData: true);
            using var reduzida = new Bitmap(DetectorMovimento.LarguraAmostra, DetectorMovimento.AlturaAmostra, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(reduzida))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.CompositingMode = CompositingMode.SourceCopy;
                g.DrawImage(imagem, new Rectangle(0, 0, DetectorMovimento.LarguraAmostra, DetectorMovimento.AlturaAmostra));
            }

            var area = new Rectangle(0, 0, reduzida.Width, reduzida.Height);
            var dados = reduzida.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var linha = new byte[dados.Stride * dados.Height];
                System.Runtime.InteropServices.Marshal.Copy(dados.Scan0, linha, 0, linha.Length);
                var cinza = new byte[reduzida.Width * reduzida.Height];
                for (var y = 0; y < reduzida.Height; y++)
                    for (var x = 0; x < reduzida.Width; x++)
                    {
                        var i = y * dados.Stride + x * 4; // BGRA
                        cinza[y * reduzida.Width + x] = (byte)((linha[i + 2] * 299 + linha[i + 1] * 587 + linha[i] * 114) / 1000);
                    }
                return cinza;
            }
            finally
            {
                reduzida.UnlockBits(dados);
            }
        }
        catch (Exception e) when (e is ArgumentException or OutOfMemoryException or ExternalException)
        {
            return null;
        }
    }
}

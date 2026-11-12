using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

namespace AAHBRANT.SST.AgenteCameras.Quadros;

// A API aceita até 4 MB por quadro. O normal (1280 px, q4) fica em ~100-300 KB; se passar do limite,
// reduz a imagem e a qualidade até caber.
[SupportedOSPlatform("windows")]
public static class LimitadorTamanho
{
    public const int LimiteBytes = 4 * 1024 * 1024;

    public static byte[] Garantir(byte[] jpeg, int limite = LimiteBytes)
    {
        if (jpeg.Length <= limite) return jpeg;
        try
        {
            using var ms = new MemoryStream(jpeg);
            using var origem = Image.FromStream(ms, false, true);
            var codec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
            var escala = 1.0;
            var qualidade = 80L;
            for (var tentativa = 0; tentativa < 6; tentativa++)
            {
                var largura = Math.Max(16, (int)(origem.Width * escala));
                var altura = Math.Max(16, (int)(origem.Height * escala));
                using var bmp = new Bitmap(largura, altura, PixelFormat.Format24bppRgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(origem, 0, 0, largura, altura);
                }
                using var saida = new MemoryStream();
                using var parametros = new EncoderParameters(1);
                parametros.Param[0] = new EncoderParameter(Encoder.Quality, qualidade);
                bmp.Save(saida, codec, parametros);
                if (saida.Length <= limite) return saida.ToArray();
                escala *= 0.75;
                qualidade = Math.Max(50, qualidade - 10);
            }
        }
        catch (Exception e) when (e is ArgumentException or OutOfMemoryException or System.Runtime.InteropServices.ExternalException)
        {
            // JPEG ilegível: devolve o original; o servidor responderá 400 e o quadro será descartado.
        }
        return jpeg;
    }
}

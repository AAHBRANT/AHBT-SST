using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace AAHBRANT.SST.AgenteBiometria.Leitores;

// Converte a captura bruta do leitor (tons de cinza, 1 byte por pixel) em PNG, para guardar como
// evidência visual da assinatura por digital no Cofre de Assinaturas. Devolve null se o tamanho não
// bater com a imagem do FS80H (ex.: leitor simulado), em vez de gerar uma imagem distorcida.
public static class ImagemDigitalPng
{
    public static byte[]? Converter(byte[] capturaBruta, int largura = SourceAfisFingerprintMatcher.Largura, int altura = SourceAfisFingerprintMatcher.Altura)
    {
        if (capturaBruta.Length != largura * altura) return null;

        using var bitmap = new Bitmap(largura, altura, PixelFormat.Format8bppIndexed);

        var paleta = bitmap.Palette;
        for (var i = 0; i < 256; i++) paleta.Entries[i] = Color.FromArgb(i, i, i);
        bitmap.Palette = paleta;

        var dados = bitmap.LockBits(new Rectangle(0, 0, largura, altura), ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
        try
        {
            // Stride é alinhado a 4 bytes: copia linha a linha.
            for (var y = 0; y < altura; y++)
                Marshal.Copy(capturaBruta, y * largura, dados.Scan0 + y * dados.Stride, largura);
        }
        finally
        {
            bitmap.UnlockBits(dados);
        }

        using var saida = new MemoryStream();
        bitmap.Save(saida, ImageFormat.Png);
        return saida.ToArray();
    }
}

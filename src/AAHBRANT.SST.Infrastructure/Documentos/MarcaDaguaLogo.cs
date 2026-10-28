using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace AAHBRANT.SST.Infrastructure.Documentos;

// Prepara a logo cadastrada na Obra para servir de marca d'água de fundo (PGR, modelo A, 27/10/2026).
// O QuestPDF não aplica opacidade a imagens, então o clareamento é feito na própria imagem: o que é
// claro (o fundo branco de um PNG/JPG) continua branco e o que é escuro vira um tom bem suave da cor
// original. Assim a marca d'água funciona com logo de fundo branco e de fundo transparente, e o
// arquivo cadastrado na Obra não é alterado. A imagem fica sobre o papel branco, atrás do texto.
internal static class MarcaDaguaLogo
{
    private const int LarguraMaxima = 1000;

    private static byte Misturar(byte canal, float peso) =>
        (byte)Math.Clamp((int)MathF.Round(255f - (255f - canal) * peso), 0, 255);

    public static byte[]? Preparar(byte[]? logo, float opacidade)
    {
        if (logo is null || logo.Length == 0) return null;

        try
        {
            using var imagem = Image.Load<Rgba32>(logo);
            if (imagem.Width > LarguraMaxima)
                imagem.Mutate(x => x.Resize(LarguraMaxima, 0));

            imagem.ProcessPixelRows(linhas =>
            {
                for (var y = 0; y < linhas.Height; y++)
                {
                    foreach (ref var pixel in linhas.GetRowSpan(y))
                    {
                        // Mistura cada pixel com o branco do papel. A transparência do PNG não serve: o QuestPDF
                        // recodifica a imagem e descarta o canal alfa, então o resultado é opaco e já clareado.
                        var luminancia = (0.299f * pixel.R + 0.587f * pixel.G + 0.114f * pixel.B) / 255f;
                        var peso = (1f - luminancia) * (pixel.A / 255f) * opacidade;
                        pixel.R = Misturar(pixel.R, peso);
                        pixel.G = Misturar(pixel.G, peso);
                        pixel.B = Misturar(pixel.B, peso);
                        pixel.A = 255;
                    }
                }
            });

            using var saida = new MemoryStream();
            imagem.SaveAsPng(saida);
            return saida.ToArray();
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException or NotSupportedException)
        {
            // Logo ilegível: o PGR sai sem marca d'água em vez de falhar a geração do documento.
            return null;
        }
    }
}

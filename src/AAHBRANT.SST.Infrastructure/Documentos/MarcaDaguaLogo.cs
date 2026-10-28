using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace AAHBRANT.SST.Infrastructure.Documentos;

// Imagem pronta para a marca d'água e a largura em que ela deve ser desenhada na folha (pontos).
internal sealed record MarcaDagua(byte[] Png, float Largura);

// Prepara a logo cadastrada na Obra para servir de marca d'água de fundo (PGR, modelo A, 27/10/2026).
// O QuestPDF não aplica opacidade a imagens, então o clareamento é feito na própria imagem: o que é
// claro (o fundo branco de um PNG/JPG) continua branco e o que é escuro vira um tom bem suave da cor
// original. Assim a marca d'água funciona com logo de fundo branco e de fundo transparente, e o
// arquivo cadastrado na Obra não é alterado. A imagem fica sobre o papel branco, atrás do texto.
//
// Logos largas e baixas (ex.: a da AAHBRANT, ~4,6:1) ocupam pouca área e sumiam com a opacidade das
// logos mais "quadradas" (pedido do usuário, 27/10: "mais forte"). Por isso a logo panorâmica é
// desenhada mais larga e a opacidade sobe conforme a área de tinta que ela efetivamente ocupa na
// folha, até um teto. As logos já encorpadas (Ponte Rio Cuiá, Parque do Roger) ficam como estavam.
internal static class MarcaDaguaLogo
{
    private const int LarguraMaximaImagem = 1000;
    private const float LarguraPadrao = 370;
    private const float LarguraPanoramica = 470; // largura útil do texto numa A4 com margem de 2 cm
    private const float AspectoPanoramico = 1.8f;
    private const float AreaDeTintaReferencia = 20000; // pt² de tinta das logos que já ficam bem como estão
    private const float ReforcoMaximo = 2.5f;

    private static byte Misturar(byte canal, float peso) =>
        (byte)Math.Clamp((int)MathF.Round(255f - (255f - canal) * peso), 0, 255);

    private static float Peso(Rgba32 pixel) =>
        (1f - (0.299f * pixel.R + 0.587f * pixel.G + 0.114f * pixel.B) / 255f) * (pixel.A / 255f);

    public static MarcaDagua? Preparar(byte[]? logo, float opacidade)
    {
        if (logo is null || logo.Length == 0) return null;

        try
        {
            using var imagem = Image.Load<Rgba32>(logo);
            if (imagem.Width > LarguraMaximaImagem)
                imagem.Mutate(x => x.Resize(LarguraMaximaImagem, 0));

            var aspecto = imagem.Width / (float)imagem.Height;
            var largura = aspecto >= AspectoPanoramico ? LarguraPanoramica : LarguraPadrao;

            double somaTinta = 0;
            imagem.ProcessPixelRows(linhas =>
            {
                for (var y = 0; y < linhas.Height; y++)
                    foreach (var pixel in linhas.GetRowSpan(y))
                        somaTinta += Peso(pixel);
            });
            var cobertura = somaTinta / ((double)imagem.Width * imagem.Height);
            var areaDeTinta = (float)cobertura * largura * (largura / aspecto);
            var reforco = areaDeTinta <= 0 ? 1f : Math.Clamp(AreaDeTintaReferencia / areaDeTinta, 1f, ReforcoMaximo);
            var opacidadeFinal = Math.Min(opacidade * reforco, 0.3f);

            imagem.ProcessPixelRows(linhas =>
            {
                for (var y = 0; y < linhas.Height; y++)
                {
                    foreach (ref var pixel in linhas.GetRowSpan(y))
                    {
                        // Mistura cada pixel com o branco do papel. A transparência do PNG não serve: o QuestPDF
                        // recodifica a imagem e descarta o canal alfa, então o resultado é opaco e já clareado.
                        var peso = Peso(pixel) * opacidadeFinal;
                        pixel.R = Misturar(pixel.R, peso);
                        pixel.G = Misturar(pixel.G, peso);
                        pixel.B = Misturar(pixel.B, peso);
                        pixel.A = 255;
                    }
                }
            });

            using var saida = new MemoryStream();
            imagem.SaveAsPng(saida);
            return new MarcaDagua(saida.ToArray(), largura);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException or NotSupportedException)
        {
            // Logo ilegível: o PGR sai sem marca d'água em vez de falhar a geração do documento.
            return null;
        }
    }
}

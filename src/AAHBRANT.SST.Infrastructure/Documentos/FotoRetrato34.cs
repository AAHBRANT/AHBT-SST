using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace AAHBRANT.SST.Infrastructure.Documentos;

// Foto facial no padrão 3×4 em pé (04/11, ISO/IEC 19794-5 / ICAO) para os PDFs. As fotos novas já
// chegam em 3×4; as antigas, gravadas deitadas, são cortadas no centro só para a exibição — o arquivo
// guardado (e o hash da evidência) não muda. Imagem em pé ou inválida volta como está.
public static class FotoRetrato34
{
    public static byte[] Cortar(byte[] imagem)
    {
        try
        {
            using var img = Image.Load(imagem);
            var alvoLargura = (int)Math.Round(img.Height * 3.0 / 4.0);
            if (img.Width <= alvoLargura + 2) return imagem;
            img.Mutate(x => x.Crop(new Rectangle((img.Width - alvoLargura) / 2, 0, alvoLargura, img.Height)));
            using var saida = new MemoryStream();
            img.Save(saida, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 90 });
            return saida.ToArray();
        }
        catch
        {
            return imagem;
        }
    }
}

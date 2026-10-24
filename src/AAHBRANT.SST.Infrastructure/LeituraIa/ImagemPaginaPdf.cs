using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace AAHBRANT.SST.Infrastructure.LeituraIa;

// Imagem de cada página digitalizada, pronta para o OCR pela IA (21/10/2026). PDF escaneado (scanner,
// CamScanner, foto do celular) guarda a página como uma imagem grande — aqui pega a maior imagem da
// página, decodifica (JPEG direto; os outros formatos via PNG do PdfPig) e devolve um JPEG reduzido.
// Sem renderizar o PDF: nada de biblioteca nativa no contêiner. Formato que não dá para decodificar
// (JBIG2, JPEG 2000) volta nulo e a página fica para preenchimento manual.
public static class ImagemPaginaPdf
{
    // Lado maior da imagem enviada: legível para a IA em letra de contrato e sem estourar o tamanho da
    // requisição (uma página A4 a 200 dpi tem ~2300 px de altura).
    public const int LadoMaximo = 2000;
    private const int QualidadeJpeg = 80;
    // Imagem menor que isto é logotipo/carimbo, não a página escaneada.
    private const int LadoMinimo = 400;

    // indicesPagina começam em 0. Página sem imagem aproveitável não entra no resultado.
    public static Dictionary<int, byte[]> ExtrairJpegs(byte[] pdf, IEnumerable<int> indicesPagina)
    {
        var resultado = new Dictionary<int, byte[]>();
        using var documento = PdfDocument.Open(pdf);
        foreach (var indice in indicesPagina.Distinct())
        {
            if (indice < 0 || indice >= documento.NumberOfPages) continue;
            try
            {
                var imagem = documento.GetPage(indice + 1).GetImages()
                    .Where(i => !i.IsImageMask && Math.Max(i.WidthInSamples, i.HeightInSamples) >= LadoMinimo)
                    .OrderByDescending(i => (long)i.WidthInSamples * i.HeightInSamples)
                    .FirstOrDefault();
                var bytes = imagem is null ? null : BytesDecodificaveis(imagem);
                if (bytes is not null) resultado[indice] = Reduzir(bytes);
            }
            catch (Exception)
            {
                // Imagem corrompida ou em formato que o ImageSharp não lê: a página só fica sem OCR.
            }
        }
        return resultado;
    }

    private static byte[]? BytesDecodificaveis(IPdfImage imagem)
    {
        var bruto = imagem.RawMemory.Span;
        if (bruto.Length > 3 && bruto[0] == 0xFF && bruto[1] == 0xD8 && bruto[2] == 0xFF) return bruto.ToArray();
        return imagem.TryGetPng(out var png) ? png : null;
    }

    public static byte[] Reduzir(byte[] imagem)
    {
        using var img = Image.Load(imagem);
        var maior = Math.Max(img.Width, img.Height);
        if (maior > LadoMaximo)
        {
            var fator = (double)LadoMaximo / maior;
            img.Mutate(x => x.Resize((int)Math.Round(img.Width * fator), (int)Math.Round(img.Height * fator)));
        }
        using var saida = new MemoryStream();
        img.SaveAsJpeg(saida, new JpegEncoder { Quality = QualidadeJpeg });
        return saida.ToArray();
    }
}

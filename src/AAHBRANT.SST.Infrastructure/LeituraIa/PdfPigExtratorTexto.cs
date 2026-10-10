using AAHBRANT.SST.Application.LeituraIa;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace AAHBRANT.SST.Infrastructure.LeituraIa;

// Texto do PDF por página, na ordem de leitura (ContentOrderTextExtractor quebra linha entre blocos, o
// que mantém as linhas das tabelas do PGR/PCMSO separadas — o texto "cru" da página emenda tudo).
public class PdfPigExtratorTexto : IExtratorTextoPdf
{
    public IReadOnlyList<string> ExtrairPaginas(byte[] pdf)
    {
        using var documento = PdfDocument.Open(pdf);
        var paginas = new List<string>();
        foreach (var pagina in documento.GetPages())
        {
            string texto;
            try
            {
                texto = ContentOrderTextExtractor.GetText(pagina);
            }
            catch (Exception)
            {
                texto = pagina.Text;
            }
            paginas.Add(texto);
        }
        return paginas;
    }
}

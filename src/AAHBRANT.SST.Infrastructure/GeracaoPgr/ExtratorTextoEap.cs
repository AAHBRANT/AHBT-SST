using System.Text;
using System.Xml.Linq;
using AAHBRANT.SST.Application.GeracaoPgr;
using AAHBRANT.SST.Application.LeituraIa;
using ClosedXML.Excel;

namespace AAHBRANT.SST.Infrastructure.GeracaoPgr;

// Transforma o arquivo da EAP em texto para a IA. Excel: uma linha por linha preenchida (colunas
// separadas por " | "), de todas as abas. MS Project: o XML exportado (Arquivo › Salvar como › XML),
// uma linha por tarefa com WBS, nível e datas; o .mpp binário não é lido. PDF: texto por página.
public class ExtratorTextoEap : IExtratorTextoEap
{
    // Limite do que vai para a IA numa chamada (EAP grande vira texto longo).
    private const int MaxCaracteres = 60_000;

    private readonly IExtratorTextoPdf _pdf;

    public ExtratorTextoEap(IExtratorTextoPdf pdf) => _pdf = pdf;

    public string Extrair(byte[] conteudo, string nomeArquivo, string contentType)
    {
        var extensao = Path.GetExtension(nomeArquivo).ToLowerInvariant();
        var texto = extensao switch
        {
            ".xlsx" or ".xlsm" => LerExcel(conteudo),
            ".xml" => LerMsProjectXml(conteudo),
            ".pdf" => string.Join("\n", _pdf.ExtrairPaginas(conteudo)),
            ".csv" or ".txt" => Encoding.UTF8.GetString(conteudo),
            ".mpp" => throw new InvalidOperationException(
                "O arquivo .mpp do MS Project não pode ser lido direto. No Project, use Arquivo › Salvar como › XML e envie o .xml."),
            ".xls" => throw new InvalidOperationException("Planilha no formato antigo (.xls). Salve como .xlsx e envie de novo."),
            _ => throw new InvalidOperationException("Formato não suportado. Envie Excel (.xlsx), XML do MS Project ou PDF."),
        };
        return texto.Length <= MaxCaracteres ? texto : texto[..MaxCaracteres];
    }

    private static string LerExcel(byte[] conteudo)
    {
        using var stream = new MemoryStream(conteudo);
        using var livro = new XLWorkbook(stream);
        var sb = new StringBuilder();
        foreach (var aba in livro.Worksheets)
        {
            sb.AppendLine($"# Aba: {aba.Name}");
            foreach (var linha in aba.RowsUsed())
            {
                var celulas = linha.CellsUsed().Select(c => c.GetFormattedString().Trim()).Where(v => v.Length > 0).ToList();
                if (celulas.Count > 0) sb.AppendLine(string.Join(" | ", celulas));
            }
        }
        return sb.ToString();
    }

    private static string LerMsProjectXml(byte[] conteudo)
    {
        XDocument doc;
        try
        {
            using var stream = new MemoryStream(conteudo);
            doc = XDocument.Load(stream);
        }
        catch (Exception)
        {
            throw new InvalidOperationException("O XML enviado não pôde ser lido. Exporte de novo pelo MS Project (Salvar como › XML).");
        }

        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;
        var tarefas = doc.Descendants(ns + "Task").ToList();
        if (tarefas.Count == 0)
            throw new InvalidOperationException("O XML não tem tarefas do MS Project.");

        var sb = new StringBuilder();
        foreach (var t in tarefas)
        {
            var nome = t.Element(ns + "Name")?.Value?.Trim();
            if (string.IsNullOrEmpty(nome)) continue;
            var wbs = t.Element(ns + "WBS")?.Value ?? t.Element(ns + "OutlineNumber")?.Value;
            var nivel = t.Element(ns + "OutlineLevel")?.Value;
            var inicio = t.Element(ns + "Start")?.Value;
            var fim = t.Element(ns + "Finish")?.Value;
            sb.AppendLine($"{wbs} | nível {nivel} | {nome} | {inicio?[..Math.Min(10, inicio.Length)]} a {fim?[..Math.Min(10, fim.Length)]}");
        }
        return sb.ToString();
    }
}

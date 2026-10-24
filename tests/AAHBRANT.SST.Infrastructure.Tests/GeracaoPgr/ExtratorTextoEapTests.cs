using System.Text;
using AAHBRANT.SST.Application.LeituraIa;
using AAHBRANT.SST.Infrastructure.GeracaoPgr;
using ClosedXML.Excel;

namespace AAHBRANT.SST.Infrastructure.Tests.GeracaoPgr;

public class ExtratorTextoEapTests
{
    private sealed class PdfFalso : IExtratorTextoPdf
    {
        public IReadOnlyList<string> ExtrairPaginas(byte[] pdf) => new[] { "pagina 1" };
    }

    private readonly ExtratorTextoEap _extrator = new(new PdfFalso());

    [Fact]
    public void Excel_UmaLinhaPorLinhaPreenchida()
    {
        using var livro = new XLWorkbook();
        var aba = livro.AddWorksheet("EAP");
        aba.Cell(1, 1).Value = "1";
        aba.Cell(1, 2).Value = "Infraestrutura";
        aba.Cell(2, 1).Value = "1.1";
        aba.Cell(2, 2).Value = "Escavação";
        using var ms = new MemoryStream();
        livro.SaveAs(ms);

        var texto = _extrator.Extrair(ms.ToArray(), "eap.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        Assert.Contains("# Aba: EAP", texto);
        Assert.Contains("1 | Infraestrutura", texto);
        Assert.Contains("1.1 | Escavação", texto);
    }

    [Fact]
    public void MsProjectXml_LeTarefasComWbsNivelEDatas()
    {
        const string xml = """
            <Project xmlns="http://schemas.microsoft.com/project">
              <Tasks>
                <Task><Name>Superestrutura</Name><WBS>2</WBS><OutlineLevel>1</OutlineLevel><Start>2027-01-04T08:00:00</Start><Finish>2027-06-30T17:00:00</Finish></Task>
                <Task><Name>Lajes</Name><WBS>2.1</WBS><OutlineLevel>2</OutlineLevel></Task>
              </Tasks>
            </Project>
            """;

        var texto = _extrator.Extrair(Encoding.UTF8.GetBytes(xml), "eap.xml", "text/xml");

        Assert.Contains("2 | nível 1 | Superestrutura | 2027-01-04 a 2027-06-30", texto);
        Assert.Contains("2.1 | nível 2 | Lajes", texto);
    }

    [Theory]
    [InlineData("eap.mpp")]
    [InlineData("eap.xls")]
    [InlineData("eap.docx")]
    public void FormatoNaoSuportado_ExplicaComoEnviar(string nome)
        => Assert.Throws<InvalidOperationException>(() => _extrator.Extrair(new byte[] { 1 }, nome, "application/octet-stream"));
}

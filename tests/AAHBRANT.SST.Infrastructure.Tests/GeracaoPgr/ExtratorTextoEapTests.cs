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

    private static byte[] PlanilhaOrcamento(int composicoes, int caracteresPorComposicao = 600)
    {
        using var livro = new XLWorkbook();
        var aba = livro.AddWorksheet("Planilha EAP");
        var cab = new[] { "item", "tipo", "codigo_origem", "descricao", "unidade", "quantidade" };
        for (var i = 0; i < cab.Length; i++) aba.Cell(1, i + 1).Value = cab[i];
        var linha = 2;
        void Item(string codigo, string nome)
        {
            aba.Cell(linha, 1).Value = codigo;
            aba.Cell(linha, 2).Value = "ITEM EAP";
            aba.Cell(linha, 4).Value = nome;
            linha++;
        }
        Item("01.01.3.1", "Rede de Água");
        for (var i = 0; i < composicoes; i++)
        {
            aba.Cell(linha, 1).Value = $"01.01.3.1.{i + 1}";
            aba.Cell(linha, 2).Value = "COMPOSIÇÃO";
            aba.Cell(linha, 4).Value = new string('X', caracteresPorComposicao);
            linha++;
        }
        Item("01.01.3.9", "Galeria");
        Item("01.01.4.6", "OAE");
        using var ms = new MemoryStream();
        livro.SaveAs(ms);
        return ms.ToArray();
    }

    [Fact]
    public void Excel_PlanilhaDeOrcamento_IgnoraComposicoes_EChegaAoUltimoItemDaEap()
    {
        // 853 composições de 600 caracteres passam de 500 mil caracteres: antes o texto era cortado e os últimos itens sumiam.
        var texto = _extrator.Extrair(PlanilhaOrcamento(853), "eap.xlsx", "application/x");

        Assert.Contains("Rede de Água", texto);
        Assert.Contains("Galeria", texto);
        Assert.Contains("OAE", texto);
        Assert.DoesNotContain("XXXX", texto);
    }

    [Fact]
    public void Excel_SemColunaTipo_MantemTodasAsLinhas()
    {
        using var livro = new XLWorkbook();
        var aba = livro.AddWorksheet("EAP");
        aba.Cell(1, 1).Value = "1";
        aba.Cell(1, 2).Value = "COMPOSIÇÃO de laje";
        using var ms = new MemoryStream();
        livro.SaveAs(ms);

        var texto = _extrator.Extrair(ms.ToArray(), "eap.xlsx", "application/x");

        Assert.Contains("COMPOSIÇÃO de laje", texto);
    }

    [Fact]
    public void Excel_GrandeDemaisMesmoSemComposicoes_RecusaEmVezDeCortarEmSilencio()
    {
        using var livro = new XLWorkbook();
        var aba = livro.AddWorksheet("EAP");
        for (var i = 1; i <= 800; i++)
        {
            aba.Cell(i, 1).Value = $"1.{i}";
            aba.Cell(i, 2).Value = new string('Y', 100);
        }
        using var ms = new MemoryStream();
        livro.SaveAs(ms);

        var ex = Assert.Throws<InvalidOperationException>(() => _extrator.Extrair(ms.ToArray(), "eap.xlsx", "application/x"));
        Assert.Contains("grande demais", ex.Message);
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

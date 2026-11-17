using AAHBRANT.SST.Application.Acidentes;
using AAHBRANT.SST.Infrastructure.Documentos;

namespace AAHBRANT.SST.Infrastructure.Tests.Documentos;

// PDF da investigação com os blocos novos (Testemunhas, Pessoas externas, Conclusão): o CPF sai
// mascarado e o telefone não entra no relatório.
public class InvestigacaoOcorrenciaPdfPessoaExternaTests
{
    static InvestigacaoOcorrenciaPdfPessoaExternaTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    [Fact]
    public void Gerar_ComTestemunhasPessoaExternaEConclusao_GeraPdf()
    {
        var modelo = Modelo(
            new[] { "Maria Souza" },
            new[] { new InvestigacaoOcorrenciaPdfPessoaExternaModelo("Carlos Visitante", "Visitante", "Fornecedor X", "***.***.***-25", 41, "Com lesão", "Corte no braço", "Primeiros socorros") },
            "Sem ações adicionais: causa tratada no local.");

        var pdf = new InvestigacaoOcorrenciaPdfService().Gerar(modelo);

        Assert.True(pdf.Length > 1000);
        var destino = Environment.GetEnvironmentVariable("INVESTIGACAO_PDF_AMOSTRA_EXTERNA");
        if (!string.IsNullOrEmpty(destino)) File.WriteAllBytes(destino, pdf);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void Gerar_SemBlocosOpcionais_ContinuaGerando()
    {
        var pdf = new InvestigacaoOcorrenciaPdfService().Gerar(Modelo(null, null, null));
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    private static byte[] QrPng()
    {
        using var g = new QRCoder.QRCodeGenerator();
        using var d = g.CreateQrCode("https://exemplo/validar/abc", QRCoder.QRCodeGenerator.ECCLevel.M);
        return new QRCoder.PngByteQRCode(d).GetGraphic(10);
    }

    private static InvestigacaoOcorrenciaPdfModelo Modelo(
        IReadOnlyList<string>? testemunhas, IReadOnlyList<InvestigacaoOcorrenciaPdfPessoaExternaModelo>? pessoas, string? conclusao) => new(
        "Obra Teste", null, "Ocorrência com pessoa externa", new DateTime(2026, 11, 17), new TimeSpan(9, 30, 0), "Portaria", null,
        new List<string>(), "Aguardando laudo", null, "Visitante escorregou na rampa.", "Sem afastamento", false, null, null, 0,
        null, null, null, null, null, new List<InvestigacaoOcorrenciaPdfAcaoModelo>(), null, new List<InvestigacaoOcorrenciaPdfFotoModelo>(),
        "ABC123", "https://exemplo/validar/abc", QrPng(), false, null, testemunhas, pessoas, conclusao);
}

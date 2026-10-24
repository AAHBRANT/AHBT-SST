using AAHBRANT.SST.Application.EntregasEpi;
using AAHBRANT.SST.Infrastructure.Documentos;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace AAHBRANT.SST.Infrastructure.Tests.Documentos;

// Foto do cabeçalho da Ficha de EPI: solta, sem legenda, recortada em 3x4 (pedido de 08/10).
public class FichaEpiFoto3x4Tests
{
    static FichaEpiFoto3x4Tests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    private static byte[] FotoSintetica(int largura, int altura)
    {
        using var img = new Image<Rgba32>(largura, altura, new Rgba32(70, 90, 120));
        for (var y = 0; y < altura; y++)
            for (var x = 0; x < largura; x++)
            {
                var dx = (x - largura / 2.0) / (largura * 0.18); var dy = (y - altura * 0.4) / (altura * 0.25);
                if (dx * dx + dy * dy < 1) img[x, y] = new Rgba32(201, 155, 122);
            }
        using var ms = new MemoryStream();
        img.Save(ms, new JpegEncoder());
        return ms.ToArray();
    }

    private static byte[] QrFalso()
    {
        using var img = new Image<Rgba32>(40, 40, new Rgba32(30, 30, 30));
        using var ms = new MemoryStream();
        img.Save(ms, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
        return ms.ToArray();
    }

    private static FichaEpiPdfModelo Modelo(byte[]? foto) => new(
        "CONSORCIO PONTE RIO CUIA", "CONSORCIO PONTE RIO CUIA", "57.622.394/0001-37", null, null,
        "Augusto Inácio Felipe", "***.***.***-34", "29", "Ajudante Geral", "Diurno", new DateTime(2026, 7, 1),
        new List<LinhaEntregaEpiPdf>(), new List<LinhaDevolucaoEpiPdf>(),
        "9F3A1C7B20E48D5A61B7C0E2F44A9D13B8E5C0A7D6F21E3B4A5C6D7E8F901234", "https://sst.exemplo/validar/abc", QrFalso(),
        TrabalhadorCpf: "661.501.594-34", TrabalhadorFoto: foto);

    [Theory]
    [InlineData(640, 480)]  // paisagem (foto de câmera)
    [InlineData(480, 640)]  // retrato
    [InlineData(300, 300)]  // quadrada
    public void Gerar_ComFotoDeQualquerProporcao_NaoQuebra(int largura, int altura)
    {
        var pdf = new EntregaEpiPdfService().Gerar(Modelo(FotoSintetica(largura, altura)));

        Assert.True(pdf.Length > 1000);
        SalvarSeSolicitado(pdf, $"foto-{largura}x{altura}");
    }

    [Fact]
    public void Gerar_SemFotoOuFotoInvalida_NaoQuebra()
    {
        var servico = new EntregaEpiPdfService();

        Assert.True(servico.Gerar(Modelo(null)).Length > 1000);
        Assert.True(servico.Gerar(Modelo(new byte[] { 1, 2, 3 })).Length > 1000);
    }

    // Para conferência visual: FICHA_PDF_SAIDA=<pasta> grava os PDFs gerados.
    private static void SalvarSeSolicitado(byte[] pdf, string nome)
    {
        var pasta = Environment.GetEnvironmentVariable("FICHA_PDF_SAIDA");
        if (string.IsNullOrEmpty(pasta)) return;
        Directory.CreateDirectory(pasta);
        File.WriteAllBytes(Path.Combine(pasta, nome + ".pdf"), pdf);
    }
}

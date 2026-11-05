using AAHBRANT.SST.Application.SessoesTreinamento;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Documentos;

namespace AAHBRANT.SST.Infrastructure.Tests.Documentos;

// Lista de presença da turma (05/11): dados da turma em grade com bordas, mesmo padrão da
// Investigação de Ocorrência.
public class AtaSessaoTreinamentoPdfServiceTests
{
    static AtaSessaoTreinamentoPdfServiceTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    [Fact]
    public void Gerar_ComParticipantes_GeraPdf()
    {
        var modelo = new AtaSessaoTreinamentoPdfModelo(
            "Ponte Rio Cuiá", null, "NR-35 Trabalho em Altura", "NR-35",
            new DateTime(2026, 10, 28), 8, "GILTON PEREIRA MIRANDA", "CERT-2026-0006",
            new DateTime(2026, 10, 28, 17, 3, 0),
            new[]
            {
                new AtaSessaoTreinamentoPdfParticipanteModelo("Ana Almeida Pereira", "AUR-0101", new DateTime(2026, 10, 28, 13, 5, 0, DateTimeKind.Utc), MetodoAutenticacaoAssinatura.ReconhecimentoFacial),
                new AtaSessaoTreinamentoPdfParticipanteModelo("Vitor Pereira Ribeiro", "AUR-0100", null),
            },
            Array.Empty<byte[]>(), "abc123", "https://exemplo/validar", PngBranco());

        var pdf = new AtaSessaoTreinamentoPdfService().Gerar(modelo);

        Assert.True(pdf.Length > 0);
        var destino = Environment.GetEnvironmentVariable("ATA_PDF_AMOSTRA");
        if (!string.IsNullOrEmpty(destino)) File.WriteAllBytes(destino, pdf);
    }

    private static byte[] PngBranco()
    {
        using var img = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(4, 4);
        using var ms = new MemoryStream();
        SixLabors.ImageSharp.ImageExtensions.SaveAsPng(img, ms);
        return ms.ToArray();
    }
}

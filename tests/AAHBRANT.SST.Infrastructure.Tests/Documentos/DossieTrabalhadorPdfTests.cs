using AAHBRANT.SST.Application.Trabalhadores;
using AAHBRANT.SST.Infrastructure.Documentos;
using AAHBRANT.SST.Infrastructure.Trabalhadores;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AAHBRANT.SST.Infrastructure.Tests.Documentos;

public class DossieTrabalhadorPdfTests
{
    static DossieTrabalhadorPdfTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    private static byte[] FotoDeitada1280x720()
    {
        using var imagem = new Image<Rgb24>(1280, 720, new Rgb24(90, 120, 150));
        using var ms = new MemoryStream();
        imagem.SaveAsJpeg(ms);
        return ms.ToArray();
    }

    [Fact]
    public void GerarIndice_ComItensIncluidosENaoIncluidos_GeraPdf()
    {
        var servico = new DossieTrabalhadorPdfService();
        var itens = new List<ItemIndiceDossie>
        {
            new("Certificados de treinamento", "Certificado — NR-35", new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc), true),
            new("Turmas de treinamento", "Lista de presença — NR-35", new DateTime(2026, 11, 5, 10, 0, 0, DateTimeKind.Utc), true),
            new("DDS", "DDS", new DateTime(2026, 11, 5, 7, 10, 0, DateTimeKind.Utc), true),
            new("Outros documentos assinados", "APR", new DateTime(2026, 11, 5, 10, 12, 0, DateTimeKind.Utc), true),
            new("Outros documentos assinados", "Certificado de assinaturas — APR", new DateTime(2026, 11, 5, 10, 12, 0, DateTimeKind.Utc), true),
            new("Outros documentos assinados", "Permissão de Trabalho", new DateTime(2026, 11, 5, 10, 12, 0, DateTimeKind.Utc), true),
            new("Outros documentos assinados", "Certificado de assinaturas — Permissão de Trabalho", new DateTime(2026, 11, 5, 10, 12, 0, DateTimeKind.Utc), true),
            new("Ficha de EPI", "Ficha de EPI", null, true),
            new("ASO — Atestado de Saúde Ocupacional", "ASO Admissional", null, false, "Arquivo danificado."),
        };

        var pdf = servico.GerarIndice("Fulano de Tal", "Obra Teste", null, itens);

        Assert.True(pdf.Length > 0);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        var pasta = Environment.GetEnvironmentVariable("FICHA_PDF_SAIDA");
        if (!string.IsNullOrEmpty(pasta))
        {
            Directory.CreateDirectory(pasta);
            File.WriteAllBytes(Path.Combine(pasta, "dossie-indice.pdf"), pdf);
        }
    }

    [Fact]
    public void ImagemComoPdf_FotoValida_GeraPdf_EArquivoInvalido_DevolveNulo()
    {
        var servico = new DossieTrabalhadorPdfService();

        var pdf = servico.ImagemComoPdf("ASO Admissional", FotoDeitada1280x720(), "Obra Teste", null);
        Assert.NotNull(pdf);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf!, 0, 4));

        Assert.Null(servico.ImagemComoPdf("ASO", new byte[] { 0x50, 0x4B, 3, 4 }, "Obra Teste", null));
    }
}

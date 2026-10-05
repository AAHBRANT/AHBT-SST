using System.Text.RegularExpressions;
using AAHBRANT.SST.Application.Dds;
using AAHBRANT.SST.Infrastructure.Documentos;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace AAHBRANT.SST.Infrastructure.Tests.Documentos;

// Registro Fotográfico do DDS (05/10): as 3 fotos de evidência saem no PDF, entre a lista de
// presença e a assinatura do responsável.
public class DdsPdfServiceTests
{
    static DdsPdfServiceTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    [Fact]
    public void Gerar_ComTresFotos_IncluiSecaoRegistroFotograficoEmUmaPagina()
    {
        var pdf = new DdsPdfService().Gerar(CriarModelo(comFotos: true));

        Assert.Equal(1, ContarPaginas(pdf));
        // Salva uma cópia para conferência visual quando a variável de ambiente estiver definida.
        var destino = Environment.GetEnvironmentVariable("DDS_PDF_AMOSTRA");
        if (!string.IsNullOrEmpty(destino)) File.WriteAllBytes(destino, pdf);
    }

    [Fact]
    public void Gerar_SemFotos_NaoQuebra()
    {
        var pdf = new DdsPdfService().Gerar(CriarModelo(comFotos: false));

        Assert.True(pdf.Length > 0);
    }

    private static DdsPdfModelo CriarModelo(bool comFotos)
    {
        var fotos = comFotos
            ? new[]
            {
                new DdsPdfFotoModelo(Jpeg(200, 120, 60), new DateTimeOffset(2026, 10, 5, 10, 5, 0, TimeSpan.Zero), -180, "Frente de serviço, bloco B", -7.1150, -34.8610),
                new DdsPdfFotoModelo(Jpeg(60, 120, 200), new DateTimeOffset(2026, 10, 5, 10, 6, 0, TimeSpan.Zero), -180, "Equipe reunida", -7.1150, -34.8610),
                new DdsPdfFotoModelo(Jpeg(60, 180, 90), null, null, "Quadro do tema do dia", null, null),
            }
            : null;

        return new DdsPdfModelo(
            "Obra Exemplo", null, new DateTime(2026, 10, 5), "Responsável Exemplo",
            new[] { new DdsPdfTemaModelo("Trabalho em altura", "Queda de nível", "Trabalho acima de 2 m", "Lesões graves", "Cinto e linha de vida", null) },
            null, null,
            Array.Empty<(string, bool)>(),
            new[]
            {
                new DdsPdfParticipanteModelo("João da Silva", new DateTime(2026, 10, 5, 10, 12, 0, DateTimeKind.Utc)),
                new DdsPdfParticipanteModelo("Marcos Pereira", new DateTime(2026, 10, 5, 10, 13, 0, DateTimeKind.Utc)),
                new DdsPdfParticipanteModelo("Carlos Souza", new DateTime(2026, 10, 5, 10, 14, 0, DateTimeKind.Utc)),
            },
            "DDS-0000", new string('a', 64), "https://exemplo.local/validar/x", Jpeg(0, 0, 0), true,
            Fotos: fotos);
    }

    private static byte[] Jpeg(byte r, byte g, byte b)
    {
        using var imagem = new Image<Rgb24>(800, 600, new Rgb24(r, g, b));
        using var ms = new MemoryStream();
        imagem.Save(ms, new JpegEncoder());
        return ms.ToArray();
    }

    private static int ContarPaginas(byte[] pdf)
    {
        var texto = System.Text.Encoding.Latin1.GetString(pdf);
        return Regex.Matches(texto, @"/Count (\d+)").Select(m => int.Parse(m.Groups[1].Value)).Max();
    }
}

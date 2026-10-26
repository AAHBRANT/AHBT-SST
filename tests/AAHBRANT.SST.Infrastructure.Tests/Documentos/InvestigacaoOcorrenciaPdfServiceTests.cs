using AAHBRANT.SST.Application.Acidentes;
using AAHBRANT.SST.Infrastructure.Documentos;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace AAHBRANT.SST.Infrastructure.Tests.Documentos;

// Relatório de Investigação de Ocorrência (23/10): gera com e sem os blocos opcionais (reunião,
// fotos, plano de ação) sem quebrar.
public class InvestigacaoOcorrenciaPdfServiceTests
{
    static InvestigacaoOcorrenciaPdfServiceTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    [Fact]
    public void Gerar_Completo_GeraPdf()
    {
        var pdf = new InvestigacaoOcorrenciaPdfService().Gerar(CriarModelo(completo: true));

        Assert.True(pdf.Length > 0);
        // Salva uma cópia para conferência visual quando a variável de ambiente estiver definida.
        var destino = Environment.GetEnvironmentVariable("INVESTIGACAO_PDF_AMOSTRA");
        if (!string.IsNullOrEmpty(destino)) File.WriteAllBytes(destino, pdf);
    }

    [Fact]
    public void Gerar_SemReuniaoFotosNemAcoes_NaoQuebra()
    {
        var pdf = new InvestigacaoOcorrenciaPdfService().Gerar(CriarModelo(completo: false));

        Assert.True(pdf.Length > 0);
    }

    // Mapa corporal da lesão (24/10): partes marcadas saem desenhadas e listadas na seção de consequências.
    [Fact]
    public void Gerar_ComPartesDoCorpoMarcadas_GeraPdfMaiorQueSemMapa()
    {
        var servico = new InvestigacaoOcorrenciaPdfService();
        var semMapa = servico.Gerar(CriarModelo(completo: true));
        var comMapa = servico.Gerar(CriarModelo(completo: true) with { RegioesCorpo = new[] { "F:mao-esq", "F:olho-dir", "C:lombar-dir" } });

        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(comMapa, 0, 4));
        Assert.True(comMapa.Length > semMapa.Length);
    }

    private static InvestigacaoOcorrenciaPdfModelo CriarModelo(bool completo) => new(
        "Obra Exemplo", null, "Quase acidente", new DateTime(2026, 10, 21), new TimeSpan(9, 40, 0),
        "Bloco B, pavimento 2, fachada norte", completo ? "Montagem de andaime fachadeiro" : null,
        completo ? new[] { "Trabalhador Exemplo A", "Trabalhador Exemplo B" } : Array.Empty<string>(),
        completo ? "Investigação concluída" : "Em investigação",
        completo ? new DateTime(2026, 10, 22) : null,
        "Durante a montagem do andaime, uma peça de travamento escorregou do pavimento superior e caiu na área de circulação, sem atingir ninguém.",
        "Sem afastamento", false, null, null, 0, null, null,
        "Potencial de lesão grave por queda de material",
        completo ? "5 Porquês" : null,
        completo ? "Ausência de rodapé no andaime; material solto sobre a plataforma; área inferior sem isolamento." : null,
        completo
            ? new[]
            {
                new InvestigacaoOcorrenciaPdfAcaoModelo("Instalar rodapé em todas as plataformas do andaime", "NR-18", "Corretiva", "Técnico Exemplo", new DateTime(2026, 10, 24), "Concluída", true),
                new InvestigacaoOcorrenciaPdfAcaoModelo("Isolar e sinalizar a área abaixo de serviços em altura", null, "Preventiva", "Encarregado Exemplo", new DateTime(2026, 10, 20), "Vencida", false),
            }
            : Array.Empty<InvestigacaoOcorrenciaPdfAcaoModelo>(),
        completo ? new InvestigacaoOcorrenciaPdfReuniaoModelo(new DateTime(2026, 10, 22, 14, 0, 0), new DateTime(2026, 10, 22, 15, 0, 0), new[] { "Engenheiro Exemplo", "Técnico Exemplo" }, true,
            new DateTime(2026, 10, 22, 14, 5, 0), new[] { "Engenheiro Exemplo", "Técnico Exemplo" }, "Causas confirmadas: falta de rodapé e área sem isolamento. Mantidas as 2 ações do plano; prazo do isolamento antecipado para 25/10.") : null,
        completo
            ? new[]
            {
                new InvestigacaoOcorrenciaPdfFotoModelo(Jpeg(200, 120, 60), new DateTime(2026, 10, 21, 12, 52, 0, DateTimeKind.Utc), -7.11500, -34.86100),
                new InvestigacaoOcorrenciaPdfFotoModelo(Jpeg(60, 120, 200), null, null, null),
                new InvestigacaoOcorrenciaPdfFotoModelo(Jpeg(60, 180, 90), new DateTime(2026, 10, 21, 12, 55, 0, DateTimeKind.Utc), null, null),
                new InvestigacaoOcorrenciaPdfFotoModelo(Jpeg(150, 150, 150), new DateTime(2026, 10, 21, 12, 56, 0, DateTimeKind.Utc), -7.11500, -34.86100),
            }
            : Array.Empty<InvestigacaoOcorrenciaPdfFotoModelo>(),
        new string('a', 64), "https://exemplo.local/validar/x", Jpeg(0, 0, 0), false);

    private static byte[] Jpeg(byte r, byte g, byte b)
    {
        using var imagem = new Image<Rgb24>(800, 600, new Rgb24(r, g, b));
        using var ms = new MemoryStream();
        imagem.Save(ms, new JpegEncoder());
        return ms.ToArray();
    }
}

using AAHBRANT.SST.Application.EntregasEpi;
using AAHBRANT.SST.Application.Trabalhadores.Queries;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Trabalhadores;

namespace AAHBRANT.SST.Infrastructure.Tests.Documentos;

// Página de resumo do Relatório de Fiscalização (cards + gráficos do perfil). Os números seguem as
// regras de PerfilGeralTab.tsx — se mudarem lá, mudam aqui.
public class RelatorioFiscalizacaoResumoTests
{
    static RelatorioFiscalizacaoResumoTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    private static readonly DateTime Hoje = new(2026, 10, 6);

    [Fact]
    public void ContarStatusEpis_SeparaEmDiaVencendoEVencido()
    {
        var perfil = CriarPerfil(episAtivos: new[]
        {
            Epi(validade: null),                  // sem validade: em dia
            Epi(validade: Hoje.AddDays(31)),      // em dia
            Epi(validade: Hoje.AddDays(30)),      // limite: ainda vencendo
            Epi(validade: Hoje),                  // vence hoje: vencendo
            Epi(validade: Hoje.AddDays(-1)),      // vencido
        });

        var (emDia, vencendo, vencido) = ResumoGraficosFiscalizacao.ContarStatusEpis(perfil, Hoje);

        Assert.Equal(2, emDia);
        Assert.Equal(2, vencendo);
        Assert.Equal(1, vencido);
    }

    [Fact]
    public void GerarSvgRosca_FatiaUnica_DesenhaCirculoCompleto_ESemFatiaZeradaNoCaminho()
    {
        var svg = ResumoGraficosFiscalizacao.GerarSvgRosca(new[]
        {
            new ResumoGraficosFiscalizacao.FatiaRosca("Em dia", 4, "#2e7d4f"),
            new ResumoGraficosFiscalizacao.FatiaRosca("Vencido", 0, "#b3261e"),
        });

        Assert.DoesNotContain("<path", svg);
        Assert.Contains("#2e7d4f", svg);
        Assert.DoesNotContain("#b3261e", svg);
    }

    [Fact]
    public void GerarSvgRosca_DuasFatias_UsaUmArcoPorFatia()
    {
        var svg = ResumoGraficosFiscalizacao.GerarSvgRosca(new[]
        {
            new ResumoGraficosFiscalizacao.FatiaRosca("Participou", 18, "#2e7d4f"),
            new ResumoGraficosFiscalizacao.FatiaRosca("Não participou", 2, "#b3261e"),
        });

        Assert.Equal(2, svg.Split("<path").Length - 1);
    }

    [Fact]
    public void Gerar_PerfilCompleto_ProduzPdf()
    {
        var perfil = CriarPerfil(
            episAtivos: new[] { Epi(Hoje.AddDays(90)), Epi(Hoje.AddDays(10)), Epi(Hoje.AddDays(-5)) },
            assiduidade: new AssiduidadeDdsDto(20, 18),
            motivos: new List<MotivoTrocaEpiDto> { new(MotivoEntregaEpi.Dano, 2), new(MotivoEntregaEpi.Extravio, 1) },
            frequencia: new List<FrequenciaTrocaEpiDto>
            {
                new(Guid.NewGuid(), "Luva de vaqueta", 3), new(Guid.NewGuid(), "Capacete classe B", 2), new(Guid.NewGuid(), "Óculos de proteção", 1),
            });

        var pdf = new RelatorioFiscalizacaoPdfService().Gerar(perfil);

        Assert.True(pdf.Length > 1000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));

        // Permite conferir o visual: ARQUIVO_PDF_RELATORIO=<caminho> dotnet test --filter ...
        var destino = Environment.GetEnvironmentVariable("ARQUIVO_PDF_RELATORIO");
        if (!string.IsNullOrWhiteSpace(destino)) File.WriteAllBytes(destino, pdf);
    }

    [Fact]
    public void Gerar_SemNenhumDado_ProduzPdfComAvisosEmVezDeGraficos()
    {
        var pdf = new RelatorioFiscalizacaoPdfService().Gerar(CriarPerfil());

        Assert.True(pdf.Length > 1000);
        var destino = Environment.GetEnvironmentVariable("ARQUIVO_PDF_RELATORIO_VAZIO");
        if (!string.IsNullOrWhiteSpace(destino)) File.WriteAllBytes(destino, pdf);
    }

    // Grade de identificação do relatório (mesma da Ficha de EPI), com foto de cadastro à direita.
    private static byte[] FotoDeTeste()
    {
        using var img = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(200, 260);
        for (var y = 0; y < 260; y++)
            for (var x = 0; x < 200; x++)
            {
                var dx = (x - 100) / 62.0; var dy = (y - 110) / 78.0;
                img[x, y] = dx * dx + dy * dy < 1
                    ? new SixLabors.ImageSharp.PixelFormats.Rgba32(201, 155, 122)
                    : (y > 215 ? new SixLabors.ImageSharp.PixelFormats.Rgba32(74, 106, 134) : new SixLabors.ImageSharp.PixelFormats.Rgba32(122, 106, 90));
            }
        using var ms = new MemoryStream();
        img.Save(ms, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder());
        return ms.ToArray();
    }

    [Fact]
    public void Gerar_ComIdentificacaoEFoto_ProduzPdfComGrade()
    {
        var perfil = CriarPerfil(episAtivos: new[] { Epi(Hoje.AddDays(90)) }, assiduidade: new AssiduidadeDdsDto(20, 18));
        var identificacao = new AAHBRANT.SST.Application.Trabalhadores.IdentificacaoRelatorioFiscalizacao(
            FotoDeTeste(), "CONSORCIO PONTE RIO CUIA", "57.622.394/0001-37");

        var pdf = new RelatorioFiscalizacaoPdfService().Gerar(perfil, identificacao);

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        var destino = Environment.GetEnvironmentVariable("ARQUIVO_PDF_RELATORIO_IDENT");
        if (!string.IsNullOrWhiteSpace(destino)) File.WriteAllBytes(destino, pdf);
    }

    [Fact]
    public void Gerar_SemFotoNemDadosDaObra_UsaAvisosEmVezDeQuebrar()
    {
        var pdf = new RelatorioFiscalizacaoPdfService().Gerar(CriarPerfil(), identificacao: null);

        Assert.True(pdf.Length > 1000);
    }

    [Fact]
    public void Gerar_FotoCorrompida_NaoDerrubaORelatorio()
    {
        var identificacao = new AAHBRANT.SST.Application.Trabalhadores.IdentificacaoRelatorioFiscalizacao(
            new byte[] { 1, 2, 3, 4 }, null, null);

        var pdf = new RelatorioFiscalizacaoPdfService().Gerar(CriarPerfil(), identificacao);

        Assert.True(pdf.Length > 1000);
    }

    private static EntregaEpiDto Epi(DateTime? validade) => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Hoje.AddDays(-60), null, validade, 1, null, null, null, null, null, null, null);

    private static PerfilCompletoTrabalhadorDto CriarPerfil(
        IEnumerable<EntregaEpiDto>? episAtivos = null,
        AssiduidadeDdsDto? assiduidade = null,
        List<MotivoTrocaEpiDto>? motivos = null,
        List<FrequenciaTrocaEpiDto>? frequencia = null) => new(
        Guid.NewGuid(), "Alan Franklin dos Santos", "30", "000.000.000-00", null,
        Guid.NewGuid(), "CONSORCIO PONTE RIO CUIA", Guid.NewGuid(), "Ajudante Geral",
        TipoVinculo.Clt, new DateTime(2026, 7, 1), false, false, null, "APTO",
        new List<AAHBRANT.SST.Application.Asos.AsoDto>(), (episAtivos ?? Array.Empty<EntregaEpiDto>()).ToList(), frequencia ?? new List<FrequenciaTrocaEpiDto>(),
        new List<AAHBRANT.SST.Application.Treinamentos.TreinamentoDto>(),
        assiduidade ?? new AssiduidadeDdsDto(0, 0),
        new List<RiscoExpostoDto>(), new List<OcorrenciaDto>(), new List<AssinaturaPerfilDto>(),
        (motivos ?? new List<MotivoTrocaEpiDto>()).Sum(m => m.Quantidade), motivos ?? new List<MotivoTrocaEpiDto>(),
        null, null, null, null);
}

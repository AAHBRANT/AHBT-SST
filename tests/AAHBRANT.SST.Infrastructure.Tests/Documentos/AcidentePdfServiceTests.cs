using System.Text;
using AAHBRANT.SST.Application.Acidentes;
using AAHBRANT.SST.Infrastructure.Documentos;

namespace AAHBRANT.SST.Infrastructure.Tests.Documentos;

// Relatório da ocorrência com o mapa corporal da lesão (24/10/2026).
public class AcidentePdfServiceTests
{
    static AcidentePdfServiceTests() => QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

    // O JSON do mapa é do TeamsApp; se alguém gerar um código que o backend recusa, o técnico marcaria
    // a região na tela e o registro seria rejeitado ao salvar.
    [Fact]
    public void MapaCorporal_CodigosAceitosPeloBackendEDentroDoLimite()
    {
        var codigos = MapaCorporalSvg.Ordenar(new[] { "F:cabeca" }).ToList();
        Assert.Single(codigos);

        var todos = MapaCorporalSvg.Desenhar(Array.Empty<string>()).Count;
        Assert.Equal(2, todos); // frente e costas

        foreach (var codigo in new[] { "F:olho-esq", "F:mao-dir", "C:lombar-dir", "C:gluteo-esq", "F:dedos-pe-esq" })
        {
            Assert.True(MapaCorporalSvg.Existe(codigo), codigo);
            Assert.True(RegioesCorpoAcidente.CodigoValido(codigo), codigo);
        }
    }

    [Fact]
    public void MapaCorporal_MarcaSoAVistaDaRegiao()
    {
        var vistas = MapaCorporalSvg.Desenhar(new[] { "C:lombar-dir" });
        Assert.False(vistas.Single(v => v.Id == "F").TemMarcacao);
        Assert.True(vistas.Single(v => v.Id == "C").TemMarcacao);
        Assert.Contains("#C4262E", vistas.Single(v => v.Id == "C").Svg);
        Assert.DoesNotContain("#C4262E", vistas.Single(v => v.Id == "F").Svg);
        Assert.Equal("Região lombar (lado direito)", MapaCorporalSvg.Rotulo("C:lombar-dir"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Gerar_ProduzPdfComESemPartesMarcadas(bool comRegioes)
    {
        var pdf = new AcidentePdfService().Gerar(Modelo(comRegioes ? new[] { "F:mao-esq", "F:olho-dir", "C:lombar-dir" } : Array.Empty<string>()));
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.True(pdf.Length > 5_000);
    }

    private static AcidentePdfModelo Modelo(IReadOnlyList<string> regioes) => new(
        "Obra Teste", null, "Acidente", "Registrado", new DateTime(2026, 10, 23), new TimeSpan(14, 0, 0),
        "Escada do bloco B", "Alvenaria", new[] { "José Carlos" },
        "Servente escorregou na escada molhada e torceu o tornozelo.", "Entorse no tornozelo direito", regioes,
        "Não conseguiu apoiar o pé", "UPA", true, 3, "Com afastamento", 0, null, "5 Porquês", "Piso molhado sem sinalização",
        new[] { new AcidentePdfAcaoModelo("Corretiva", "Sinalizar piso molhado", "Alta", new DateTime(2026, 10, 30), "Técnico", "Pendente") },
        Array.Empty<byte[]>(), new string('a', 64), "https://exemplo/validar/abc", PngMinimo(), false);

    // O rodapé desenha o QR como imagem: precisa ser um PNG válido.
    private static byte[] PngMinimo()
    {
        using var imagem = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(8, 8);
        using var saida = new MemoryStream();
        SixLabors.ImageSharp.ImageExtensions.SaveAsPng(imagem, saida);
        return saida.ToArray();
    }
}

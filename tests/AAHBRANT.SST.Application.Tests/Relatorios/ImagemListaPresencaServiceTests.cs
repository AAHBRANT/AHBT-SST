using AAHBRANT.SST.Application.Relatorios;
using AAHBRANT.SST.Infrastructure.Documentos;
using SixLabors.ImageSharp;

namespace AAHBRANT.SST.Application.Tests.Relatorios;

public class ImagemListaPresencaServiceTests
{
    static ImagemListaPresencaServiceTests() => QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

    private static readonly DateTime T0 = new(2026, 10, 7, 7, 2, 0);

    private static ListaPresencaDados Dados(int presentes = 20, int ausentes = 2, int? duracao = 39, DateTime? fechadoEm = null, string? tema = "Trabalho em altura") => new(
        Guid.NewGuid(), "Obra de Teste", new DateTime(2026, 10, 7), tema, new DateTime(2026, 10, 8, 8, 0, 0), presentes + ausentes,
        Enumerable.Range(0, presentes).Select(i => new PresencaLinha($"Funcionário {i} da Silva Santos Oliveira", $"{100 + i}", T0.AddMinutes(i))).ToList(),
        Enumerable.Range(0, ausentes).Select(i => new AusenteLinha($"Ausente {i}", $"{900 + i}", i + 1)).ToList(),
        presentes > 0 ? T0 : null, duracao is null ? null : T0.AddMinutes(duracao.Value), duracao, fechadoEm, fechadoEm is null ? null : 68);

    private static (int Largura, int Altura) Gerar(ListaPresencaDados d)
    {
        var png = new ImagemListaPresencaService().Gerar(d);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png.Take(4).ToArray());
        var info = Image.Identify(png);
        return (info.Width, info.Height);
    }

    [Fact]
    public void GeraPngDe1080DeLargura()
    {
        var (largura, altura) = Gerar(Dados(fechadoEm: new DateTime(2026, 10, 7, 8, 10, 0)));

        Assert.Equal(1080, largura);
        Assert.InRange(altura, 1000, 2400);
    }

    [Fact]
    public void ComMuitaGente_MostraSoOsPrimeirosEContinuaEmUmaImagemSo()
    {
        var poucos = Gerar(Dados(presentes: 16, ausentes: 12));
        var muitos = Gerar(Dados(presentes: 200, ausentes: 150));

        // 200 presentes e 150 ausentes: a imagem corta em 16 e 12 ("e mais N no PDF"), então só cresce uma linha por lista.
        Assert.InRange(muitos.Altura - poucos.Altura, 0, 80);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(0, 3)]
    public void CasosVazios_NaoQuebram(int presentes, int ausentes)
    {
        var (largura, _) = Gerar(Dados(presentes, ausentes, duracao: null));

        Assert.Equal(1080, largura);
    }

    [Fact]
    public void SemTemaESemFechamento_Gera()
    {
        var (largura, _) = Gerar(Dados(tema: null, fechadoEm: null));

        Assert.Equal(1080, largura);
    }
}

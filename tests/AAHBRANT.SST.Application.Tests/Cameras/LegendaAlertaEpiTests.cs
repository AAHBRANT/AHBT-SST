using AAHBRANT.SST.Application.Cameras;

namespace AAHBRANT.SST.Application.Tests.Cameras;

public class LegendaAlertaEpiTests
{
    // 18:06 UTC = 15:06 em Brasília.
    private static readonly DateTime Momento = new(2026, 11, 9, 18, 6, 0, DateTimeKind.Utc);

    [Fact]
    public void SemCapacete_SemIdentificacao_MontaQuatroLinhas()
    {
        var legenda = LegendaAlertaEpi.Montar("Beira Rio", "Betoneira", Momento, 2, 1, 0);

        Assert.Equal(
            "⚠️ EPI — pessoa sem capacete\n" +
            "Obra Beira Rio · Câmera Betoneira · 09/11 15:06\n" +
            "2 pessoas na cena, 1 sem capacete\n" +
            "Trabalhador não identificado",
            legenda);
    }

    [Fact]
    public void SemColete_UsaTituloDeColete()
    {
        var legenda = LegendaAlertaEpi.Montar("Beira Rio", "Betoneira", Momento, 1, 0, 1);

        Assert.StartsWith("⚠️ EPI — pessoa sem colete\n", legenda);
        Assert.Contains("\n1 pessoa na cena, 1 sem colete\n", legenda);
    }

    [Fact]
    public void SemOsDois_CitaCapaceteEColete()
    {
        var legenda = LegendaAlertaEpi.Montar("Beira Rio", "Betoneira", Momento, 3, 2, 1);

        Assert.StartsWith("⚠️ EPI — pessoa sem capacete e sem colete\n", legenda);
        Assert.Contains("\n3 pessoas na cena, 2 sem capacete, 1 sem colete\n", legenda);
    }

    [Fact]
    public void TrabalhadorIdentificado_CitaNomeNaUltimaLinha()
    {
        var legenda = LegendaAlertaEpi.Montar("Beira Rio", "Betoneira", Momento, 1, 1, 0, " João da Silva ");

        Assert.EndsWith("\nTrabalhador: João da Silva (identificado pelo rosto)", legenda);
    }

    [Fact]
    public void Cena_ListaIdentificadosComOQueFaltaEContaOsDemais()
    {
        var legenda = LegendaAlertaEpi.MontarCena("Beira Rio", "Portaria", Momento, 4, 2, 2, new[]
        {
            new IdentificadoNaCena("João da Silva", true, false),
            new IdentificadoNaCena(" Pedro Souza ", true, true),
        }, naoIdentificados: 1);

        Assert.Equal(
            "⚠️ EPI — pessoa sem capacete e sem colete\n" +
            "Obra Beira Rio · Câmera Portaria · 09/11 15:06\n" +
            "4 pessoas na cena, 2 sem capacete, 2 sem colete\n" +
            "Identificados pelo rosto: João da Silva (sem capacete), Pedro Souza (sem capacete e sem colete)\n" +
            "Não identificados: 1",
            legenda);
    }

    [Fact]
    public void Cena_SemNinguemIdentificado_InformaQuantos()
    {
        var varios = LegendaAlertaEpi.MontarCena("Beira Rio", "Portaria", Momento, 3, 2, 0, Array.Empty<IdentificadoNaCena>(), 2);
        var um = LegendaAlertaEpi.MontarCena("Beira Rio", "Portaria", Momento, 3, 1, 0, Array.Empty<IdentificadoNaCena>(), 1);

        Assert.EndsWith("\nNão identificados: 2", varios);
        Assert.EndsWith("\nTrabalhador não identificado", um);
    }

    [Fact]
    public void NomeEmBranco_SaiComoNaoIdentificado()
    {
        var legenda = LegendaAlertaEpi.Montar("Beira Rio", "Betoneira", Momento, 1, 1, 0, "  ");

        Assert.EndsWith("\nTrabalhador não identificado", legenda);
    }
}

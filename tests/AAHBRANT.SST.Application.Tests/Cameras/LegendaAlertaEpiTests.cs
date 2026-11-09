using AAHBRANT.SST.Application.Cameras;

namespace AAHBRANT.SST.Application.Tests.Cameras;

public class LegendaAlertaEpiTests
{
    // 18:06 UTC = 15:06 em Brasília.
    private static readonly DateTime Momento = new(2026, 11, 9, 18, 6, 0, DateTimeKind.Utc);

    [Fact]
    public void SemCapacete_MontaTresLinhas()
    {
        var legenda = LegendaAlertaEpi.Montar("Beira Rio", "Betoneira", Momento, 2, 1, 0);

        Assert.Equal(
            "⚠️ EPI — pessoa sem capacete\n" +
            "Obra Beira Rio · Câmera Betoneira · 09/11 15:06\n" +
            "2 pessoas na cena, 1 sem capacete",
            legenda);
    }

    [Fact]
    public void SemColete_UsaTituloDeColete()
    {
        var legenda = LegendaAlertaEpi.Montar("Beira Rio", "Betoneira", Momento, 1, 0, 1);

        Assert.StartsWith("⚠️ EPI — pessoa sem colete\n", legenda);
        Assert.EndsWith("1 pessoa na cena, 1 sem colete", legenda);
    }

    [Fact]
    public void SemOsDois_CitaCapaceteEColete()
    {
        var legenda = LegendaAlertaEpi.Montar("Beira Rio", "Betoneira", Momento, 3, 2, 1);

        Assert.StartsWith("⚠️ EPI — pessoa sem capacete e sem colete\n", legenda);
        Assert.EndsWith("3 pessoas na cena, 2 sem capacete, 1 sem colete", legenda);
    }
}

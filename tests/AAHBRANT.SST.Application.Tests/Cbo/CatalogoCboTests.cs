using AAHBRANT.SST.Application.Cbo;

namespace AAHBRANT.SST.Application.Tests.Cbo;

// Tabela oficial da CBO 2002 como fonte do CBO das funções (29/10/2026).
public class CatalogoCboTests
{
    private static CatalogoCbo Catalogo() => new(new[]
    {
        ("717020", "Servente de obras", true),
        ("717020", "Ajudante de obras", false),
        ("715205", "Calceteiro", true),
        ("715205", "Rasteleiro de asfalto", false),
        ("715210", "Pedreiro", true),
        ("715215", "Pedreiro (chaminés industriais)", true),
    });

    [Theory]
    [InlineData("7170-20", "717020")]
    [InlineData("717020", "717020")]
    [InlineData("717010 - Operador de martelete", "717010")]
    [InlineData("7170", null)]
    [InlineData("Pedreiro", null)]
    [InlineData(null, null)]
    public void Normalizar_AceitaFormatosDoCadastroAntigo(string? valor, string? esperado) =>
        Assert.Equal(esperado, CodigoCbo.Normalizar(valor));

    [Fact]
    public void Formatar_DevolveQuatroDigitosTracoDois() => Assert.Equal("7170-20", CodigoCbo.Formatar("717020"));

    [Fact]
    public void Buscar_PorSinonimo_DevolveCodigoComTituloOficial()
    {
        var r = Assert.Single(Catalogo().Buscar("rastel"));
        Assert.Equal(("715205", "7152-05", "Calceteiro", "Rasteleiro de asfalto"), (r.Codigo, r.CodigoFormatado, r.Titulo, r.Sinonimo));
    }

    [Fact]
    public void Buscar_PorCodigo_EPorPalavra_UmResultadoPorCodigo()
    {
        Assert.Equal(new[] { "715205", "715210", "715215" }, Catalogo().Buscar("7152").Select(x => x.Codigo).ToArray());
        Assert.Equal("715210", Catalogo().Buscar("pedreiro")[0].Codigo); // título exato primeiro
        Assert.Single(Catalogo().Buscar("obras"));
    }

    [Fact]
    public void Existe_ETituloOficial_SoParaCodigoDaTabela()
    {
        var c = Catalogo();
        Assert.True(c.Existe("7170-20"));
        Assert.False(c.Existe("999999"));
        Assert.Equal("Servente de obras", c.TituloOficial("717020"));
    }

    [Fact]
    public void Candidatos_TrazOCodigoDoSinonimo()
    {
        Assert.Contains(Catalogo().Candidatos("Rasteleiro"), x => x.Codigo == "715205");
        Assert.Contains(Catalogo().Candidatos("Ajudante/Servente"), x => x.Codigo == "717020");
    }
}

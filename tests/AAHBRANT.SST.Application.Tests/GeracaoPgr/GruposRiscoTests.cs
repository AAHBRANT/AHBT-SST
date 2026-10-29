using AAHBRANT.SST.Application.GeracaoPgr;

namespace AAHBRANT.SST.Application.Tests.GeracaoPgr;

// Tabela de cores do mapa de riscos aplicada ao PGR (29/10/2026).
public class GruposRiscoTests
{
    [Theory]
    [InlineData("Físico", 1, "Verde")]
    [InlineData("QUIMICO", 2, "Vermelho")]
    [InlineData("Biológico", 3, "Marrom")]
    [InlineData("Ergonômico", 4, "Amarelo")]
    [InlineData("Psicossocial", 4, "Amarelo")]
    [InlineData(" acidente ", 5, "Azul")]
    public void DoTipo_ClassificaNoGrupoECorDaTabela(string tipo, int grupo, string cor)
    {
        var g = GruposRisco.DoTipo(tipo);

        Assert.NotNull(g);
        Assert.Equal(grupo, g!.Numero);
        Assert.Equal(cor, g.Cor);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Outro")]
    public void DoTipo_ForaDaTabela_SemGrupo(string? tipo) => Assert.Null(GruposRisco.DoTipo(tipo));
}

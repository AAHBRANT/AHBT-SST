using AAHBRANT.SST.Domain.Entidades;

namespace AAHBRANT.SST.Infrastructure.Tests.Documentos;

public class CatalogoNormasRegulamentadorasTests
{
    [Theory]
    [InlineData("NR-35", "NR-35")]
    [InlineData("nr 35", "NR-35")]
    [InlineData("NR35", "NR-35")]
    [InlineData("NR-35 Trabalho em Altura", "NR-35")]
    [InlineData("NR-6", "NR-06")]
    [InlineData("NR 06", "NR-06")]
    [InlineData("NR.10", "NR-10")]
    public void NormalizarNumero_AceitaVariacoesDeEscrita(string entrada, string esperado)
        => Assert.Equal(esperado, CatalogoNormasRegulamentadoras.NormalizarNumero(entrada));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Integração de Segurança")]
    public void NormalizarNumero_SemNr_DevolveNulo(string? entrada)
        => Assert.Null(CatalogoNormasRegulamentadoras.NormalizarNumero(entrada));

    [Fact]
    public void Obter_Nr35_TrazConteudoECargaHoraria()
    {
        var nr35 = CatalogoNormasRegulamentadoras.Obter("nr 35");

        Assert.NotNull(nr35);
        Assert.Equal("Trabalho em Altura", nr35.Titulo);
        Assert.Equal(8, nr35.CargaHorariaMinima);
        Assert.Equal(7, nr35.ConteudoProgramatico.Count);
    }

    [Fact]
    public void Obter_NrForaDoCatalogo_DevolveNulo()
        => Assert.Null(CatalogoNormasRegulamentadoras.Obter("NR-99"));
}

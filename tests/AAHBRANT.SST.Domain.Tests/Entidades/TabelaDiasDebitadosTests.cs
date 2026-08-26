using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Xunit;

namespace AAHBRANT.SST.Domain.Tests.Entidades;

public class TabelaDiasDebitadosTests
{
    [Theory]
    [InlineData(GravidadeAcidente.Obito, null, 6000)]
    [InlineData(GravidadeAcidente.IncapacidadePermanenteTotal, null, 6000)]
    [InlineData(GravidadeAcidente.SemAfastamento, null, 0)]
    [InlineData(GravidadeAcidente.ComAfastamento, null, 0)]
    public void Calcular_CasosFixos_RetornaValorEsperado(GravidadeAcidente gravidade, int? informado, int esperado)
    {
        Assert.Equal(esperado, TabelaDiasDebitados.Calcular(gravidade, informado));
    }

    [Fact]
    public void Calcular_IncapacidadeParcial_UsaValorInformado()
    {
        Assert.Equal(180, TabelaDiasDebitados.Calcular(GravidadeAcidente.IncapacidadePermanenteParcial, 180));
    }

    [Fact]
    public void Calcular_IncapacidadeParcial_SemValorInformado_RetornaZero()
    {
        Assert.Equal(0, TabelaDiasDebitados.Calcular(GravidadeAcidente.IncapacidadePermanenteParcial, null));
    }
}

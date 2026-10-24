using AAHBRANT.SST.Application.Dds.Commands;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Tests.Dds;

public class CriarDdsSemanalCommandValidatorTests
{
    private static CriarDdsSemanalCommand Comando(TipoDdsSemanal tipo, string? empresa) =>
        new(Guid.NewGuid(), tipo, empresa, null, new DateTime(2026, 10, 19), "oid");

    [Theory]
    [InlineData(TipoDdsSemanal.Proprios, null, true)]
    [InlineData(TipoDdsSemanal.Terceirizados, null, false)]
    [InlineData(TipoDdsSemanal.Terceirizados, "Empresa X", true)]
    [InlineData(TipoDdsSemanal.ProprioseTerceirizados, null, false)]
    [InlineData(TipoDdsSemanal.ProprioseTerceirizados, "Empresa X", true)]
    public void Validate_EmpresaTerceirizada_ObrigatoriaQuandoIncluiTerceirizados(TipoDdsSemanal tipo, string? empresa, bool valido)
    {
        var resultado = new CriarDdsSemanalCommandValidator().Validate(Comando(tipo, empresa));

        Assert.Equal(valido, resultado.IsValid);
    }

    [Fact]
    public void Validate_TipoInexistente_RetornaErro()
    {
        var resultado = new CriarDdsSemanalCommandValidator().Validate(Comando((TipoDdsSemanal)99, null));

        Assert.False(resultado.IsValid);
    }
}

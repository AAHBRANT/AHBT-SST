using AAHBRANT.SST.Application.EntregasEpi.Commands;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Tests.EntregasEpi;

public class CriarEntregaEpiCommandValidatorTests
{
    private static CriarEntregaEpiCommand ComandoValido(MotivoEntregaEpi motivo = MotivoEntregaEpi.Inicial) =>
        new(Guid.NewGuid(), Guid.NewGuid(), DateTime.Today, null, null, 1, null, null, null, motivo, null, null);

    [Fact]
    public void Validate_ComandoValido_NaoRetornaErros()
    {
        var validator = new CriarEntregaEpiCommandValidator();

        var resultado = validator.Validate(ComandoValido());

        Assert.True(resultado.IsValid);
    }

    [Theory]
    [InlineData(MotivoEntregaEpi.Inicial)]
    [InlineData(MotivoEntregaEpi.Dano)]
    [InlineData(MotivoEntregaEpi.Extravio)]
    [InlineData(MotivoEntregaEpi.Vencimento)]
    [InlineData(MotivoEntregaEpi.TrocaDeFuncao)]
    public void Validate_QualquerMotivoTipoValido_NaoRetornaErroDeMotivo(MotivoEntregaEpi motivo)
    {
        var validator = new CriarEntregaEpiCommandValidator();

        var resultado = validator.Validate(ComandoValido(motivo));

        Assert.DoesNotContain(resultado.Errors, e => e.PropertyName == nameof(CriarEntregaEpiCommand.MotivoTipo));
    }

    [Fact]
    public void Validate_MotivoTipoForaDoEnum_RetornaErro()
    {
        var validator = new CriarEntregaEpiCommandValidator();
        var comando = ComandoValido() with { MotivoTipo = (MotivoEntregaEpi)999 };

        var resultado = validator.Validate(comando);

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(CriarEntregaEpiCommand.MotivoTipo));
    }

    [Fact]
    public void Validate_NumeroListaPresencaNr6MaiorQue50Caracteres_RetornaErro()
    {
        var validator = new CriarEntregaEpiCommandValidator();
        var comando = ComandoValido() with { NumeroListaPresencaNr6 = new string('9', 51) };

        var resultado = validator.Validate(comando);

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(CriarEntregaEpiCommand.NumeroListaPresencaNr6));
    }
}

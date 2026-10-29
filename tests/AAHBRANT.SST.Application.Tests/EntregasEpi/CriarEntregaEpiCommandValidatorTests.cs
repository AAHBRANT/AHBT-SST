using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.EntregasEpi.Commands;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Tests.EntregasEpi;

public class CriarEntregaEpiCommandValidatorTests
{
    private static CriarEntregaEpiCommand ComandoValido(MotivoEntregaEpi motivo = MotivoEntregaEpi.Inicial) =>
        // "Hoje" de Brasília, o mesmo da regra NaoFutura: DateTime.Today no runner do CI (UTC) já é o
        // dia seguinte entre 21h e meia-noite de Brasília e o comando válido virava "data futura".
        new(Guid.NewGuid(), Guid.NewGuid(), DataNaoFuturaExtensions.HojeBrasilia(), null, null, 1, null, null, null, motivo, null, null);

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

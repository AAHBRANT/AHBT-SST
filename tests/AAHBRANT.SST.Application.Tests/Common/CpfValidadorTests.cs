using AAHBRANT.SST.Application.Common;

namespace AAHBRANT.SST.Application.Tests.Common;

public class CpfValidadorTests
{
    [Theory]
    [InlineData("52998224725")]
    [InlineData("11144477735")]
    public void EhValido_DeveAceitarCpfComDigitosVerificadoresCorretos(string cpf)
    {
        Assert.True(CpfValidador.EhValido(cpf));
    }

    [Theory]
    [InlineData("12345678900")]
    [InlineData("00000000000")]
    [InlineData("11111111111")]
    [InlineData("5299822472")]
    [InlineData("529982247256")]
    [InlineData("5299822472a")]
    [InlineData("")]
    [InlineData(null)]
    public void EhValido_DeveRejeitarCpfInvalido(string? cpf)
    {
        Assert.False(CpfValidador.EhValido(cpf));
    }
}

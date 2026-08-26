using AAHBRANT.SST.Application.Common;

namespace AAHBRANT.SST.Application.Tests.Common;

public class GeradorCpfFicticioTests
{
    [Fact]
    public void Gerar_DeveRetornarOnzeDigitosNumericos()
    {
        var cpf = GeradorCpfFicticio.Gerar(0);

        Assert.Equal(11, cpf.Length);
        Assert.All(cpf, c => Assert.True(char.IsDigit(c)));
    }

    [Fact]
    public void Gerar_DeveRetornarCpfComDigitoVerificadorValido()
    {
        for (var indice = 0; indice < 250; indice++)
        {
            var cpf = GeradorCpfFicticio.Gerar(indice);
            Assert.True(CpfValidador.EhValido(cpf), $"CPF inválido para índice {indice}: {cpf}");
        }
    }

    [Fact]
    public void Gerar_NaoDeveColidirParaIndicesDiferentes()
    {
        var cpfs = Enumerable.Range(0, 250).Select(GeradorCpfFicticio.Gerar).ToList();

        Assert.Equal(cpfs.Count, cpfs.Distinct().Count());
    }
}

using AAHBRANT.SST.AgenteBiometria.Leitores;

namespace AAHBRANT.SST.AgenteBiometria.Tests.Leitores;

public class SimuladoFingerprintMatcherTests
{
    [Fact]
    public void Comparar_ComArraysIdenticos_DeveRetornar100()
    {
        var matcher = new SimuladoFingerprintMatcher();
        var template = new byte[] { 1, 2, 3, 4, 5 };

        var score = matcher.Comparar(template, template);

        Assert.Equal(100, score);
    }

    [Fact]
    public void Comparar_ComArraysTotalmenteDiferentes_DeveRetornarProximoDeZero()
    {
        var matcher = new SimuladoFingerprintMatcher();

        var score = matcher.Comparar(new byte[] { 1, 1, 1, 1 }, new byte[] { 2, 2, 2, 2 });

        Assert.Equal(0, score);
    }

    [Fact]
    public void Comparar_ComArrayVazio_DeveRetornarZero()
    {
        var matcher = new SimuladoFingerprintMatcher();

        var score = matcher.Comparar(Array.Empty<byte>(), new byte[] { 1, 2, 3 });

        Assert.Equal(0, score);
    }
}

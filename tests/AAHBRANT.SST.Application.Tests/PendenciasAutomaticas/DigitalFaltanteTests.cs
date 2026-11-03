using AAHBRANT.SST.Application.PendenciasAutomaticas;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Tests.PendenciasAutomaticas;

public class DigitalFaltanteTests
{
    private static string? Faltante(params DedoBiometria[] dedos) => PadraoPendenciaAutomatica.DigitalFaltante(dedos);

    [Fact]
    public void SemDigital_FaltamAsDuasMaos() => Assert.Equal("Falta: as duas mãos", Faltante());

    [Fact]
    public void SoDireita_FaltaEsquerda() => Assert.Equal("Falta: mão esquerda", Faltante(DedoBiometria.PolegarDireito));

    [Fact]
    public void SoEsquerda_FaltaDireita() => Assert.Equal("Falta: mão direita", Faltante(DedoBiometria.PolegarEsquerdo));

    [Fact]
    public void AsDuasMaos_Completo() => Assert.Null(Faltante(DedoBiometria.PolegarDireito, DedoBiometria.PolegarEsquerdo));

    [Fact]
    public void SoAntiga_PedeAOutraMao() => Assert.StartsWith("1 digital antiga", Faltante(DedoBiometria.NaoInformado));

    [Fact]
    public void AntigaMaisUmaMao_ContaComoCompleto() => Assert.Null(Faltante(DedoBiometria.NaoInformado, DedoBiometria.PolegarDireito));

    [Fact]
    public void DuasAntigas_AindaPedeAOutraMao() =>
        Assert.NotNull(Faltante(DedoBiometria.NaoInformado, DedoBiometria.NaoInformado));
}

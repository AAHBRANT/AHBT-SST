using AAHBRANT.SST.Infrastructure.Seguranca;

namespace AAHBRANT.SST.Infrastructure.Tests.Seguranca;

public class SegredoDispositivoHasherTests
{
    [Fact]
    public void GerarSegredo_DeveRetornarStringNaoVaziaEAleatoria()
    {
        var segredo1 = SegredoDispositivoHasher.GerarSegredo();
        var segredo2 = SegredoDispositivoHasher.GerarSegredo();

        Assert.False(string.IsNullOrWhiteSpace(segredo1));
        Assert.NotEqual(segredo1, segredo2);
    }

    [Fact]
    public void Verificar_ComSegredoCorreto_DeveRetornarTrue()
    {
        var segredo = SegredoDispositivoHasher.GerarSegredo();
        var hash = SegredoDispositivoHasher.GerarHash(segredo);

        Assert.True(SegredoDispositivoHasher.Verificar(segredo, hash));
    }

    [Fact]
    public void Verificar_ComSegredoErrado_DeveRetornarFalse()
    {
        var hash = SegredoDispositivoHasher.GerarHash(SegredoDispositivoHasher.GerarSegredo());

        Assert.False(SegredoDispositivoHasher.Verificar("segredo-errado", hash));
    }
}

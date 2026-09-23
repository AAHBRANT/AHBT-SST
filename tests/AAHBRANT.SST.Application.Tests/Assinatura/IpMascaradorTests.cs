using AAHBRANT.SST.Application.Assinatura;

namespace AAHBRANT.SST.Application.Tests.Assinatura;

// A página de validação é anônima: publicar o IP inteiro de quem assinou seria expor dado pessoal a
// qualquer um que fotografe o QR. O mascaramento preserva a prova de que há registro de origem sem
// entregar o endereço.
public class IpMascaradorTests
{
    [Theory]
    [InlineData("10.20.0.44", "10.20.0.xxx")]
    [InlineData("189.45.212.7", "189.45.212.xxx")]
    public void Mascarar_IPv4_EscondeUltimoOcteto(string ip, string esperado)
    {
        Assert.Equal(esperado, IpMascarador.Mascarar(ip));
    }

    [Fact]
    public void Mascarar_IPv6_MantemApenasOsDoisPrimeirosGrupos()
    {
        Assert.Equal("2804:14d:…", IpMascarador.Mascarar("2804:14d:1287:8d00:1c2f:5a3e:9b7c:11ff"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Mascarar_SemValor_RetornaNulo(string? ip)
    {
        Assert.Null(IpMascarador.Mascarar(ip));
    }

    [Fact]
    public void Mascarar_ValorInesperado_NaoVazaOConteudo()
    {
        Assert.Equal("…", IpMascarador.Mascarar("valor-estranho"));
    }
}

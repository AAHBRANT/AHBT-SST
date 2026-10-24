using AAHBRANT.SST.Api.Autorizacao;
using Microsoft.Extensions.Configuration;

namespace AAHBRANT.SST.Api.IntegrationTests.Autorizacao;

public class ConfiguracaoAutenticacaoTests
{
    [Fact] public void ProducaoSemEntraNaoInicia()
    {
        // Fonte em memória (vazia): sem nenhuma fonte registrada, o indexador de IConfigurationRoot
        // lança "A configuration source is not registered" ao gravar um valor.
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        Assert.Throws<InvalidOperationException>(() => ConfiguracaoAutenticacao.Validar(config, false));
        ConfiguracaoAutenticacao.Validar(config, true);
        config["AzureAd:TenantId"] = "tenant";
        Assert.Throws<InvalidOperationException>(() => ConfiguracaoAutenticacao.Validar(config, false));
        config["AzureAd:ClientId"] = "client";
        ConfiguracaoAutenticacao.Validar(config, false);
    }
}

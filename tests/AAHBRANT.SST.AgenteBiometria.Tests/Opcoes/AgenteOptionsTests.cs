using AAHBRANT.SST.AgenteBiometria.Opcoes;
using Microsoft.Extensions.Configuration;

namespace AAHBRANT.SST.AgenteBiometria.Tests.Opcoes;

public class AgenteOptionsTests
{
    // No FS80H testado a detecção de dedo vivo recusou a maioria dos dedos verdadeiros; ligá-la por
    // omissão travaria o uso na obra. O padrão precisa continuar desligado.
    [Fact]
    public void DetectarDedoFalso_PorPadraoFicaDesligado()
    {
        Assert.False(new AgenteOptions().DetectarDedoFalso);
    }

    [Fact]
    public void DetectarDedoFalso_PodeSerLigadoPelaConfiguracao()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Agente:DetectarDedoFalso"] = "true" })
            .Build();

        var opcoes = config.GetSection("Agente").Get<AgenteOptions>()!;

        Assert.True(opcoes.DetectarDedoFalso);
    }

    [Fact]
    public void AppSettingsPadrao_MantemADeteccaoDesligada()
    {
        var caminho = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var config = new ConfigurationBuilder().AddJsonFile(caminho, optional: false).Build();

        Assert.False(config.GetSection("Agente").Get<AgenteOptions>()!.DetectarDedoFalso);
    }
}

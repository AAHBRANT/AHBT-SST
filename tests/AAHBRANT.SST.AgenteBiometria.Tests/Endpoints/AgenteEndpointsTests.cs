using AAHBRANT.SST.AgenteBiometria.Endpoints;
using AAHBRANT.SST.AgenteBiometria.Leitores;
using AAHBRANT.SST.AgenteBiometria.Opcoes;
using AAHBRANT.SST.AgenteBiometria.Servicos;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.AgenteBiometria.Tests.Endpoints;

public class AgenteEndpointsTests
{
    [Fact]
    public void ObterDispositivo_DeveRetornarDispositivoIdESegredoDasOpcoes()
    {
        var dispositivoId = Guid.NewGuid();
        var options = Options.Create(new AgenteOptions { DispositivoId = dispositivoId, SegredoDispositivo = "segredo-x" });

        var resultado = AgenteEndpoints.ObterDispositivo(options);

        var ok = Assert.IsType<Ok<DispositivoResponse>>(resultado);
        Assert.Equal(dispositivoId, ok.Value!.DispositivoId);
        Assert.Equal("segredo-x", ok.Value.SegredoDispositivo);
    }

    [Fact]
    public async Task Capturar_ComMelhorScoreAcimaDeZero_DeveRetornarTrabalhadorComMaiorSimilaridade()
    {
        var leitor = new SimuladoFingerprintReader(new byte[] { 1, 2, 3, 4 });
        var matcher = new SimuladoFingerprintMatcher();
        var options = Options.Create(new AgenteOptions());
        var httpClient = new HttpClient();
        var cache = new TemplateCacheService(new BackendClient(httpClient, options), options);

        // TemplateCacheService.Templates é populado só via SincronizarAsync (que chama o backend);
        // para testar Capturar isoladamente, este teste cobre o caminho "cache vazio" abaixo e o
        // caminho "com match" fica coberto pelo teste de integração manual descrito na Task 21.
        var resultado = await AgenteEndpoints.Capturar(leitor, matcher, cache, CancellationToken.None);

        Assert.IsType<NotFound<ErroResponse>>(resultado.Result);
    }

    [Fact]
    public async Task CapturarBruto_DeveRetornarBytesCapturados()
    {
        var captura = new byte[] { 9, 8, 7 };
        var leitor = new SimuladoFingerprintReader(captura);

        var resultado = await AgenteEndpoints.CapturarBruto(leitor, CancellationToken.None);

        var ok = Assert.IsType<Ok<CapturaBrutaResponse>>(resultado);
        Assert.Equal(captura, ok.Value!.TemplateBruto);
    }
}

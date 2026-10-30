using AAHBRANT.SST.AgenteBiometria.Endpoints;
using AAHBRANT.SST.AgenteBiometria.Leitores;
using AAHBRANT.SST.AgenteBiometria.Opcoes;
using AAHBRANT.SST.AgenteBiometria.Servicos;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.AgenteBiometria.Tests.Endpoints;

public class AgenteEndpointsTests
{
    // O segredo do dispositivo não pode mais sair para o navegador: com ele dava para "assinar" por qualquer um.
    [Fact]
    public void ObterDispositivo_DeveRetornarDispositivoIdSemOSegredo()
    {
        var dispositivoId = Guid.NewGuid();
        var options = Options.Create(new AgenteOptions { DispositivoId = dispositivoId, SegredoDispositivo = "segredo-x" });

        var resultado = AgenteEndpoints.ObterDispositivo(options);

        var ok = Assert.IsType<Ok<DispositivoResponse>>(resultado);
        Assert.Equal(dispositivoId, ok.Value!.DispositivoId);
        Assert.True(ok.Value.LeituraAssinada);
        Assert.DoesNotContain("segredo-x", System.Text.Json.JsonSerializer.Serialize(ok.Value));
    }

    private static ChaveAssinaturaAgente NovaChave() =>
        new(Options.Create(new AgenteOptions { CaminhoChaveAssinatura = Path.Combine(Path.GetTempPath(), $"chave-teste-{Guid.NewGuid():N}.bin") }));

    // A chave fica gravada: reiniciar o agente não pode trocar a chave (o servidor recusaria a nova).
    [Fact]
    public void ChaveAssinatura_ReabertaDoMesmoArquivo_MantemAMesmaChavePublica()
    {
        var caminho = Path.Combine(Path.GetTempPath(), $"chave-teste-{Guid.NewGuid():N}.bin");
        var opcoes = Options.Create(new AgenteOptions { CaminhoChaveAssinatura = caminho });
        using var primeira = new ChaveAssinaturaAgente(opcoes);
        using var segunda = new ChaveAssinaturaAgente(opcoes);

        Assert.Equal(primeira.ChavePublicaBase64, segunda.ChavePublicaBase64);
    }

    [Fact]
    public void ChaveAssinatura_AssinaturaConfereComAChavePublica()
    {
        using var chave = NovaChave();
        var conteudo = AgenteEndpoints.ConteudoAssinado("desafio1", Guid.NewGuid(), 87.5);

        var assinatura = chave.Assinar(conteudo);

        using var verificador = System.Security.Cryptography.ECDsa.Create();
        verificador.ImportSubjectPublicKeyInfo(Convert.FromBase64String(chave.ChavePublicaBase64), out _);
        Assert.True(verificador.VerifyData(System.Text.Encoding.UTF8.GetBytes(conteudo), Convert.FromBase64String(assinatura),
            System.Security.Cryptography.HashAlgorithmName.SHA256));
        Assert.EndsWith("|87.50", conteudo);
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
        using var chave = NovaChave();
        var resultado = await AgenteEndpoints.Capturar(leitor, matcher, cache, chave, null, CancellationToken.None);

        Assert.IsType<NotFound<ErroResponse>>(resultado.Result);
    }

    [Fact]
    public async Task CapturarBruto_DeveRetornarBytesCapturados()
    {
        var captura = new byte[] { 9, 8, 7 };
        var leitor = new SimuladoFingerprintReader(captura);

        var resultado = await AgenteEndpoints.CapturarBruto(leitor, new SimuladoFingerprintMatcher(), null, CancellationToken.None);

        var ok = Assert.IsType<Ok<CapturaBrutaResponse>>(resultado);
        Assert.Equal(captura, ok.Value!.TemplateBruto);
    }

    // Ordem de captura e pedido de "novo toque" precisam chegar ao leitor: é isso que impede confirmar a
    // digital com a mesma imagem ainda no vidro.
    private sealed class LeitorEspiao : IFingerprintReader
    {
        public List<bool> PedidosDeNovoToque { get; } = new();

        public Task<byte[]> CapturarAsync(CancellationToken ct, bool exigirNovoToque = false, Func<byte[], bool>? aceitar = null)
        {
            PedidosDeNovoToque.Add(exigirNovoToque);
            return Task.FromResult(new byte[] { 5 });
        }
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task CapturarBruto_RepassaOPedidoDeNovoToqueAoLeitor(bool? novoToque, bool esperado)
    {
        var leitor = new LeitorEspiao();

        await AgenteEndpoints.CapturarBruto(leitor, new SimuladoFingerprintMatcher(), novoToque, CancellationToken.None);

        Assert.Equal(new[] { esperado }, leitor.PedidosDeNovoToque);
    }

    [Fact]
    public void CompararTemplates_DevolveOScoreDoMatcher()
    {
        var resultado = AgenteEndpoints.CompararTemplates(
            new CompararTemplatesRequest(new byte[] { 1, 2, 3, 4 }, new byte[] { 1, 2, 3, 4 }), new SimuladoFingerprintMatcher());

        var ok = Assert.IsType<Ok<CompararTemplatesResponse>>(resultado.Result);
        Assert.Equal(100, ok.Value!.Score);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompararTemplates_ComTemplateVazio_RetornaBadRequest(bool vazioOA)
    {
        var vazio = Array.Empty<byte>();
        var cheio = new byte[] { 1 };
        var corpo = vazioOA ? new CompararTemplatesRequest(vazio, cheio) : new CompararTemplatesRequest(cheio, vazio);

        var resultado = AgenteEndpoints.CompararTemplates(corpo, new SimuladoFingerprintMatcher());

        Assert.IsType<BadRequest<ErroResponse>>(resultado.Result);
    }
}

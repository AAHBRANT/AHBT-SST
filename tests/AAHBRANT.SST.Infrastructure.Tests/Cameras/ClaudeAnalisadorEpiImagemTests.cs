using System.Net;
using System.Text.Json;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Integracao.Cameras;
using AAHBRANT.SST.Infrastructure.Integracao.SuporteIa;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Tests.Cameras;

public class ClaudeAnalisadorEpiImagemTests
{
    private sealed class HandlerFalso : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _corpoResposta;
        public string? CorpoEnviado { get; private set; }
        public HttpRequestMessage? Requisicao { get; private set; }

        public HandlerFalso(HttpStatusCode status, string corpoResposta)
        {
            _status = status;
            _corpoResposta = corpoResposta;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requisicao = request;
            CorpoEnviado = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(_status) { Content = new StringContent(_corpoResposta) };
        }
    }

    private sealed class FabricaFalsa : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public FabricaFalsa(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private static string RespostaClaude(string jsonInterno)
        => JsonSerializer.Serialize(new { content = new object[] { new { type = "text", text = jsonInterno } } });

    private static ClaudeAnalisadorEpiImagem Criar(HandlerFalso handler, string? apiKey = "chave-de-teste")
    {
        var cliente = new ClaudeChatJsonCliente(
            new FabricaFalsa(handler),
            Options.Create(new AzureOpenAiOptions()),
            Options.Create(new ClaudeOptions { ApiKey = apiKey }),
            NullLogger<ClaudeChatJsonCliente>.Instance);
        return new ClaudeAnalisadorEpiImagem(cliente);
    }

    [Fact]
    public async Task Analisar_DevolveContagensEEnviaImagemEmBase64()
    {
        var handler = new HandlerFalso(HttpStatusCode.OK,
            RespostaClaude("{\"pessoas\":2,\"semCapacete\":1,\"semColete\":0,\"confianca\":\"alta\"}"));
        var imagem = new byte[] { 0xFF, 0xD8, 0x01, 0x02 };

        var resultado = await Criar(handler).AnalisarAsync(imagem, default);

        Assert.Equal(2, resultado.Pessoas);
        Assert.Equal(1, resultado.SemCapacete);
        Assert.Equal(0, resultado.SemColete);
        Assert.Equal(ConfiancaAnaliseEpi.Alta, resultado.Confianca);
        Assert.Contains(Convert.ToBase64String(imagem), handler.CorpoEnviado);
        Assert.Contains("image/jpeg", handler.CorpoEnviado);
        Assert.EndsWith("/v1/messages", handler.Requisicao!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Analisar_NumerosIncoerentes_LancaInvalidOperationException()
    {
        var handler = new HandlerFalso(HttpStatusCode.OK,
            RespostaClaude("{\"pessoas\":1,\"semCapacete\":3,\"semColete\":0,\"confianca\":\"media\"}"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Criar(handler).AnalisarAsync(new byte[] { 0xFF, 0xD8 }, default));
    }

    [Fact]
    public async Task Analisar_FalhaDoClaude_LancaInvalidOperationException()
    {
        var handler = new HandlerFalso(HttpStatusCode.BadRequest, "erro");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Criar(handler).AnalisarAsync(new byte[] { 0xFF, 0xD8 }, default));
    }

    [Fact]
    public async Task Analisar_SemChaveConfigurada_LancaInvalidOperationException()
    {
        var handler = new HandlerFalso(HttpStatusCode.OK, RespostaClaude("{}"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Criar(handler, apiKey: null).AnalisarAsync(new byte[] { 0xFF, 0xD8 }, default));
        Assert.Null(handler.Requisicao);
    }
}

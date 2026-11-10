using AAHBRANT.SST.Application.Cameras;
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
    public async Task Analisar_ComPosicoesCoerentes_DevolveODetalheDeCadaPessoa()
    {
        var handler = new HandlerFalso(HttpStatusCode.OK, RespostaClaude(
            "{\"pessoas\":2,\"semCapacete\":1,\"semColete\":0,\"confianca\":\"alta\",\"pessoasDetalhe\":[" +
            "{\"cabecaX\":0.25,\"cabecaY\":0.3,\"semCapacete\":true,\"semColete\":false}," +
            "{\"cabecaX\":1.4,\"cabecaY\":-0.2,\"semCapacete\":false,\"semColete\":false}]}"));

        var resultado = await Criar(handler).AnalisarAsync(new byte[] { 0xFF, 0xD8, 0x01 }, default);

        Assert.NotNull(resultado.Detalhe);
        Assert.Equal(2, resultado.Detalhe!.Count);
        Assert.Equal(new PessoaNaCena(0.25, 0.3, true, false), resultado.Detalhe[0]);
        // Posição fora da imagem é trazida para a borda.
        Assert.Equal(new PessoaNaCena(1, 0, false, false), resultado.Detalhe[1]);
        Assert.Contains("pessoasDetalhe", handler.CorpoEnviado);
    }

    [Fact]
    public async Task Analisar_PosicoesQueNaoBatemComAContagem_MantemContagensSemDetalhe()
    {
        var handler = new HandlerFalso(HttpStatusCode.OK, RespostaClaude(
            "{\"pessoas\":3,\"semCapacete\":1,\"semColete\":0,\"confianca\":\"media\",\"pessoasDetalhe\":[" +
            "{\"cabecaX\":0.25,\"cabecaY\":0.3,\"semCapacete\":true,\"semColete\":false}]}"));

        var resultado = await Criar(handler).AnalisarAsync(new byte[] { 0xFF, 0xD8, 0x01 }, default);

        Assert.Equal(3, resultado.Pessoas);
        Assert.Equal(1, resultado.SemCapacete);
        Assert.Null(resultado.Detalhe);
    }

    [Fact]
    public async Task Analisar_DevolveOsTokensCobradosPelaApi()
    {
        var corpo = JsonSerializer.Serialize(new
        {
            content = new object[] { new { type = "text", text = "{\"pessoas\":0,\"semCapacete\":0,\"semColete\":0,\"confianca\":\"alta\",\"pessoasDetalhe\":[]}" } },
            usage = new { input_tokens = 1700, cache_read_input_tokens = 100, output_tokens = 45 },
        });

        var resultado = await Criar(new HandlerFalso(HttpStatusCode.OK, corpo)).AnalisarAsync(new byte[] { 0xFF, 0xD8, 0x01 }, default);

        Assert.Equal(new UsoIa(1800, 45), resultado.Uso);
    }

    // A análise roda sem parar: usa o modelo barato das câmeras, não o Claude:Modelo das demais leituras.
    [Fact]
    public async Task Analisar_UsaOModeloDasCamerasENaoOModeloPadrao()
    {
        var handler = new HandlerFalso(HttpStatusCode.OK,
            RespostaClaude("{\"pessoas\":0,\"semCapacete\":0,\"semColete\":0,\"confianca\":\"alta\"}"));

        await Criar(handler).AnalisarAsync(new byte[] { 0xFF, 0xD8, 0x01 }, default);

        Assert.Contains("\"model\":\"claude-haiku-5-5\"", handler.CorpoEnviado);
        Assert.DoesNotContain("claude-sonnet", handler.CorpoEnviado);
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

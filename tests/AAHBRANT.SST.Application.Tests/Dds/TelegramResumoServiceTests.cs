using System.Net;
using AAHBRANT.SST.Infrastructure.Integracao.Telegram;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Application.Tests.Dds;

// O resumo do DDS vai para o mesmo grupo do Suporte IA, salvo se ResumoChatId for preenchido.
public class TelegramResumoServiceTests
{
    private sealed class Captura : HttpMessageHandler
    {
        public List<string> Corpos { get; } = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Corpos.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class Fabrica : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public Fabrica(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private static TelegramResumoService Criar(Captura captura, TelegramSuporteOptions opcoes) =>
        new(new Fabrica(captura), Options.Create(opcoes), NullLogger<TelegramResumoService>.Instance);

    [Fact]
    public async Task SemResumoChatId_UsaOGrupoDoSuporte()
    {
        var captura = new Captura();

        await Criar(captura, new TelegramSuporteOptions { BotToken = "t", SuporteChatId = "grupo-sst" }).EnviarAsync("oi");

        Assert.Contains("grupo-sst", Assert.Single(captura.Corpos));
    }

    [Fact]
    public async Task ComResumoChatId_UsaOChatProprio()
    {
        var captura = new Captura();

        await Criar(captura, new TelegramSuporteOptions { BotToken = "t", SuporteChatId = "grupo-sst", ResumoChatId = "so-resumos" }).EnviarAsync("oi");

        var corpo = Assert.Single(captura.Corpos);
        Assert.Contains("so-resumos", corpo);
        Assert.DoesNotContain("grupo-sst", corpo);
    }

    [Fact]
    public async Task SemTokenOuSemChat_NaoEnvia()
    {
        var captura = new Captura();

        await Criar(captura, new TelegramSuporteOptions { BotToken = "", SuporteChatId = "grupo-sst" }).EnviarAsync("oi");
        await Criar(captura, new TelegramSuporteOptions { BotToken = "t" }).EnviarAsync("oi");

        Assert.Empty(captura.Corpos);
    }
}

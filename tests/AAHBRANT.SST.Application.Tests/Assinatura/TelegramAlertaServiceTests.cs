using System.Net;
using AAHBRANT.SST.Infrastructure.Integracao.Telegram;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Application.Tests.Assinatura;

// Alertas da assinatura saem pelo robô próprio (Telegram:AlertasBotToken) quando configurado, para o
// responsável dar som de alarme só a eles; sem ele, pelo robô dos resumos (Telegram:BotToken).
public class TelegramAlertaServiceTests
{
    private sealed class Captura : HttpMessageHandler
    {
        public List<string> Urls { get; } = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Urls.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"ok":true,"result":{"message_id":7}}""") });
        }
    }

    private sealed class Fabrica : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public Fabrica(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private static TelegramAlertaService Criar(Captura captura, TelegramSuporteOptions opcoes) =>
        new(new Fabrica(captura), Options.Create(opcoes), NullLogger<TelegramAlertaService>.Instance);

    [Fact]
    public async Task ComRoboDeAlertas_EnviaEBotoesERegistraWebhookPorEle()
    {
        var captura = new Captura();
        var servico = Criar(captura, new TelegramSuporteOptions { BotToken = "robo-dds", AlertasBotToken = "robo-alertas", AlertasChatId = "123" });

        await servico.EnviarAsync("oi");
        await servico.EnviarComBotoesAsync("aprovar?", [new("Aprovar", "pa:1:s")]);
        await servico.RegistrarWebhookAsync("https://api/x", new string('s', 32));

        Assert.Equal(3, captura.Urls.Count);
        Assert.All(captura.Urls, url => Assert.Contains("/botrobo-alertas/", url));
    }

    [Fact]
    public async Task SemRoboDeAlertas_UsaORoboDosResumos()
    {
        var captura = new Captura();

        await Criar(captura, new TelegramSuporteOptions { BotToken = "robo-dds", AlertasChatId = "123" }).EnviarAsync("oi");

        Assert.Contains("/botrobo-dds/", Assert.Single(captura.Urls));
    }
}

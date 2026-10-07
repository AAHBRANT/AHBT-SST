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

    private sealed class CapturaMultipart : HttpMessageHandler
    {
        public string? Url { get; private set; }
        public string? Corpo { get; private set; }
        public string? TipoDoConteudo { get; private set; }
        public HttpStatusCode Resposta { get; init; } = HttpStatusCode.OK;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Url = request.RequestUri!.ToString();
            TipoDoConteudo = request.Content!.Headers.ContentType!.MediaType;
            Corpo = System.Text.Encoding.Latin1.GetString(await request.Content.ReadAsByteArrayAsync(cancellationToken));
            return new HttpResponseMessage(Resposta);
        }
    }

    [Fact]
    public async Task EnviarImagem_MandaSendPhotoComChatLegendaEArquivo()
    {
        var captura = new CapturaMultipart();
        var servico = new TelegramResumoService(new Fabrica(captura), Options.Create(new TelegramSuporteOptions { BotToken = "t", SuporteChatId = "grupo-sst" }), NullLogger<TelegramResumoService>.Instance);

        var ok = await servico.EnviarImagemAsync(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "DDS encerrado, Obra Sul.");

        Assert.True(ok);
        Assert.EndsWith("/bott/sendPhoto", captura.Url);
        Assert.Equal("multipart/form-data", captura.TipoDoConteudo);
        Assert.Contains("grupo-sst", captura.Corpo);
        Assert.Contains("DDS encerrado, Obra Sul.", captura.Corpo);
        Assert.Contains("name=photo", captura.Corpo);
        Assert.Contains("resumo-dds.png", captura.Corpo);
    }

    [Fact]
    public async Task EnviarImagem_QuandoOTelegramRecusa_DevolveFalse()
    {
        var captura = new CapturaMultipart { Resposta = HttpStatusCode.BadRequest };
        var servico = new TelegramResumoService(new Fabrica(captura), Options.Create(new TelegramSuporteOptions { BotToken = "t", SuporteChatId = "grupo-sst" }), NullLogger<TelegramResumoService>.Instance);

        Assert.False(await servico.EnviarImagemAsync(new byte[] { 1 }, "x"));
    }

    [Fact]
    public async Task EnviarImagem_SemConfiguracao_NaoEnviaEDevolveFalse()
    {
        var captura = new CapturaMultipart();
        var servico = new TelegramResumoService(new Fabrica(captura), Options.Create(new TelegramSuporteOptions()), NullLogger<TelegramResumoService>.Instance);

        Assert.False(await servico.EnviarImagemAsync(new byte[] { 1 }, "x"));
        Assert.Null(captura.Url);
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

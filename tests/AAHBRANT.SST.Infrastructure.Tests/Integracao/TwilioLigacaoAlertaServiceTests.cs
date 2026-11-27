using System.Net;
using System.Text;
using AAHBRANT.SST.Infrastructure.Integracao.Twilio;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Tests.Integracao;

public class TwilioLigacaoAlertaServiceTests
{
    private sealed class HandlerFalso : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _resposta;
        public HttpRequestMessage? Requisicao { get; private set; }
        public string? CorpoEnviado { get; private set; }

        public HandlerFalso(HttpStatusCode status, string resposta)
        {
            _status = status;
            _resposta = resposta;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requisicao = request;
            CorpoEnviado = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(_status) { Content = new StringContent(_resposta) };
        }
    }

    private sealed class FabricaFalsa : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public FabricaFalsa(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private static TwilioOptions OpcoesCompletas() => new()
    {
        AccountSid = "AC123",
        AuthToken = "token-de-teste",
        NumeroOrigem = "+15550001111",
        NumeroDestino = "+5583900000000",
    };

    private static TwilioLigacaoAlertaService Criar(HandlerFalso handler, TwilioOptions opcoes)
        => new(new FabricaFalsa(handler), Options.Create(opcoes), NullLogger<TwilioLigacaoAlertaService>.Instance);

    [Fact]
    public async Task Ligar_monta_a_chamada_com_autenticacao_numeros_e_fala_em_portugues()
    {
        var handler = new HandlerFalso(HttpStatusCode.Created, """{"sid":"CA999","status":"queued"}""");

        var chamada = await Criar(handler, OpcoesCompletas()).LigarAsync("Reconhecimento facial caiu.");

        Assert.Equal("CA999", chamada);
        Assert.Equal("https://api.twilio.com/2010-04-01/Accounts/AC123/Calls.json", handler.Requisicao!.RequestUri!.ToString());
        Assert.Equal("Basic", handler.Requisicao.Headers.Authorization!.Scheme);
        Assert.Equal("AC123:token-de-teste",
            Encoding.UTF8.GetString(Convert.FromBase64String(handler.Requisicao.Headers.Authorization.Parameter!)));
        var corpo = WebUtility.UrlDecode(handler.CorpoEnviado!);
        Assert.Contains("To=+5583900000000", corpo);
        Assert.Contains("From=+15550001111", corpo);
        Assert.Contains("language=\"pt-BR\"", corpo);
        Assert.Contains("Reconhecimento facial caiu.", corpo);
    }

    [Fact]
    public async Task Ligar_sem_configuracao_nao_chama_o_twilio()
    {
        var handler = new HandlerFalso(HttpStatusCode.Created, "{}");
        var servico = Criar(handler, new TwilioOptions { AccountSid = "AC123", AuthToken = "x" });

        Assert.False(servico.Configurada);
        Assert.Null(await servico.LigarAsync("texto"));
        Assert.Null(handler.Requisicao);
    }

    [Fact]
    public async Task Ligar_recusada_devolve_null()
    {
        var handler = new HandlerFalso(HttpStatusCode.BadRequest, """{"code":21219,"message":"unverified"}""");

        Assert.Null(await Criar(handler, OpcoesCompletas()).LigarAsync("texto"));
    }

    [Fact]
    public void Twiml_escapa_o_texto_e_le_duas_vezes()
    {
        var twiml = TwilioLigacaoAlertaService.MontarTwiml("Obra <A> & \"B\"");

        Assert.Contains("Obra &lt;A&gt; &amp; &quot;B&quot;", twiml);
        Assert.DoesNotContain("<A>", twiml);
        Assert.Equal(3, twiml.Split("<Say ").Length);
    }
}

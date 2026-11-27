using System.Net.Http.Headers;
using System.Security;
using System.Text;
using System.Text.Json;
using AAHBRANT.SST.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Integracao.Twilio;

// Seção "Twilio". AccountSid e AuthToken são segredos (secretref no Container App). Números no formato
// internacional: NumeroOrigem é o número comprado no Twilio; NumeroDestino é o celular do responsável.
public class TwilioOptions
{
    public string? AccountSid { get; set; }
    public string? AuthToken { get; set; }
    public string? NumeroOrigem { get; set; }
    public string? NumeroDestino { get; set; }
}

public sealed class TwilioLigacaoAlertaService : ILigacaoAlertaService
{
    public const string NomeCliente = "twilio";
    public const int TamanhoMaximoTexto = 500;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TwilioOptions _opcoes;
    private readonly ILogger<TwilioLigacaoAlertaService> _logger;

    public TwilioLigacaoAlertaService(IHttpClientFactory httpClientFactory, IOptions<TwilioOptions> opcoes, ILogger<TwilioLigacaoAlertaService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _opcoes = opcoes.Value;
        _logger = logger;
    }

    public bool Configurada =>
        !string.IsNullOrWhiteSpace(_opcoes.AccountSid) && !string.IsNullOrWhiteSpace(_opcoes.AuthToken)
        && !string.IsNullOrWhiteSpace(_opcoes.NumeroOrigem) && !string.IsNullOrWhiteSpace(_opcoes.NumeroDestino);

    // Voz neural em português; o texto é lido duas vezes, com pausa, para quem atende ainda acordando.
    public static string MontarTwiml(string texto)
    {
        var limpo = texto.Trim();
        if (limpo.Length > TamanhoMaximoTexto)
            limpo = limpo[..TamanhoMaximoTexto];
        var fala = $"<Say language=\"pt-BR\" voice=\"Polly.Camila-Neural\">{SecurityElement.Escape(limpo)}</Say>";
        return $"<Response><Pause length=\"1\"/>{fala}<Pause length=\"2\"/>{fala}</Response>";
    }

    public async Task<string?> LigarAsync(string texto, CancellationToken ct = default)
    {
        if (!Configurada)
        {
            _logger.LogWarning("Ligação de alerta não feita: configure Twilio:AccountSid, AuthToken, NumeroOrigem e NumeroDestino.");
            return null;
        }

        var cliente = _httpClientFactory.CreateClient(NomeCliente);
        using var pedido = new HttpRequestMessage(HttpMethod.Post,
            $"https://api.twilio.com/2010-04-01/Accounts/{Uri.EscapeDataString(_opcoes.AccountSid!)}/Calls.json");
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_opcoes.AccountSid}:{_opcoes.AuthToken}")));
        pedido.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["To"] = _opcoes.NumeroDestino!,
            ["From"] = _opcoes.NumeroOrigem!,
            ["Twiml"] = MontarTwiml(texto),
            // Toca até 40 s antes de desistir; caixa postal não conta como atendida.
            ["Timeout"] = "40",
        });

        using var resposta = await cliente.SendAsync(pedido, ct);
        var conteudo = await resposta.Content.ReadAsStringAsync(ct);
        if (!resposta.IsSuccessStatusCode)
        {
            _logger.LogWarning("Twilio recusou a ligação de alerta: {StatusCode} {Erro}", resposta.StatusCode, conteudo);
            return null;
        }

        using var json = JsonDocument.Parse(conteudo);
        return json.RootElement.TryGetProperty("sid", out var sid) ? sid.GetString() : "";
    }
}

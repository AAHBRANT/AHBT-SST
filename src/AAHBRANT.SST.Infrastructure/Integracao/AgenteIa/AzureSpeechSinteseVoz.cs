using System.Net.Http.Headers;
using System.Security;
using System.Text;
using AAHBRANT.SST.Application.AgenteIa;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Integracao.AgenteIa;

public class AzureSpeechOptions
{
    /// <summary>Região do recurso de Speech (hml: sst-speech-hml em brazilsouth).</summary>
    public string Regiao { get; set; } = "brazilsouth";
    public string? ApiKey { get; set; }
    public string Voz { get; set; } = "pt-BR-FranciscaNeural";
    /// <summary>Velocidade da fala em SSML (ex.: "+5%").</summary>
    public string Velocidade { get; set; } = "+5%";
}

/// <summary>
/// Voz neural do Azure AI Speech via REST (sem SDK). O áudio não é salvo: volta direto para o app.
/// </summary>
public class AzureSpeechSinteseVoz : ISinteseVoz
{
    public const string NomeCliente = "azure-speech";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<AzureSpeechOptions> _options;
    private readonly ILogger<AzureSpeechSinteseVoz> _logger;

    public AzureSpeechSinteseVoz(IHttpClientFactory httpClientFactory, IOptions<AzureSpeechOptions> options, ILogger<AzureSpeechSinteseVoz> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public bool Configurado => !string.IsNullOrWhiteSpace(_options.Value.ApiKey);

    public async Task<byte[]> SintetizarAsync(string texto, CancellationToken ct)
    {
        if (!Configurado)
            throw new InvalidOperationException("Voz neural não configurada. Configure AzureSpeech:ApiKey.");

        var o = _options.Value;
        var ssml = MontarSsml(texto, o.Voz, o.Velocidade);
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, $"https://{o.Regiao}.tts.speech.microsoft.com/cognitiveservices/v1")
        {
            Content = new StringContent(ssml, Encoding.UTF8),
        };
        requisicao.Content.Headers.ContentType = new MediaTypeHeaderValue("application/ssml+xml");
        requisicao.Headers.Add("Ocp-Apim-Subscription-Key", o.ApiKey);
        requisicao.Headers.Add("X-Microsoft-OutputFormat", "audio-24khz-48kbitrate-mono-mp3");
        requisicao.Headers.UserAgent.ParseAdd("gsst-api");

        var cliente = _httpClientFactory.CreateClient(NomeCliente);
        using var resposta = await cliente.SendAsync(requisicao, ct);
        if (!resposta.IsSuccessStatusCode)
        {
            var corpo = await resposta.Content.ReadAsStringAsync(ct);
            _logger.LogError("Falha na voz neural do Azure ({Status}): {Corpo}", (int)resposta.StatusCode, corpo);
            throw new InvalidOperationException("Não foi possível gerar a voz agora.");
        }
        return await resposta.Content.ReadAsByteArrayAsync(ct);
    }

    public static string MontarSsml(string texto, string voz, string velocidade) =>
        "<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"pt-BR\">" +
        $"<voice name=\"{SecurityElement.Escape(voz)}\"><prosody rate=\"{SecurityElement.Escape(velocidade)}\">" +
        SecurityElement.Escape(texto) +
        "</prosody></voice></speak>";
}

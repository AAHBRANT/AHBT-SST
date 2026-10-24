using System.Net.Http.Headers;
using System.Net.Http.Json;
using AAHBRANT.SST.Application.SuporteIa;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Integracao.SuporteIa;

public class AzureOpenAiOptions
{
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public string DeploymentTranscricao { get; set; } = "transcricao";
    public string ApiVersion { get; set; } = "2025-03-01-preview";
    public string DeploymentChat { get; set; } = "gpt5mini";
    public string ApiVersionChat { get; set; } = "2025-04-01-preview";
}

/// <summary>
/// Transcrição via deployment gpt-4o-transcribe do Azure OpenAI (recurso compartilhado
/// oai-gpol-hml-27207f, deployment "transcricao"). Chamada REST direta para não trazer o SDK
/// inteiro por um único endpoint.
/// </summary>
public class AzureOpenAiTranscricaoService : ITranscricaoAudioService
{
    // Vocabulário do SST passado como dica ao modelo — melhora siglas que a fala costuma deturpar.
    private const string DicaVocabulario =
        "Sistema SST da AAHBRANT: DDS, APR, PT, EPI, EPC, NR-35, NR-10, NR-06, PGR, PCMSO, CIPA, ASO, obra, inspeção, treinamento.";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<AzureOpenAiOptions> _options;
    private readonly ILogger<AzureOpenAiTranscricaoService> _logger;

    public AzureOpenAiTranscricaoService(
        IHttpClientFactory httpClientFactory,
        IOptions<AzureOpenAiOptions> options,
        ILogger<AzureOpenAiTranscricaoService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public bool Configurado
        => !string.IsNullOrWhiteSpace(_options.Value.Endpoint) && !string.IsNullOrWhiteSpace(_options.Value.ApiKey);

    public async Task<string> TranscreverAsync(Stream audio, string nomeArquivo, string? contentType, CancellationToken ct)
    {
        if (!Configurado)
            throw new InvalidOperationException("Transcrição de áudio não configurada. Configure AzureOpenAI:Endpoint e AzureOpenAI:ApiKey.");

        var opcoes = _options.Value;
        var url = $"{opcoes.Endpoint!.TrimEnd('/')}/openai/deployments/{opcoes.DeploymentTranscricao}/audio/transcriptions?api-version={opcoes.ApiVersion}";

        using var conteudoAudio = new StreamContent(audio);
        conteudoAudio.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Split(';')[0]);

        using var formulario = new MultipartFormDataContent
        {
            { conteudoAudio, "file", nomeArquivo },
            { new StringContent("pt"), "language" },
            { new StringContent("json"), "response_format" },
            { new StringContent(DicaVocabulario), "prompt" },
        };

        using var requisicao = new HttpRequestMessage(HttpMethod.Post, url) { Content = formulario };
        requisicao.Headers.Add("api-key", opcoes.ApiKey);

        var cliente = _httpClientFactory.CreateClient();
        cliente.Timeout = TimeSpan.FromSeconds(120);
        using var resposta = await cliente.SendAsync(requisicao, ct);

        if (!resposta.IsSuccessStatusCode)
        {
            var corpo = await resposta.Content.ReadAsStringAsync(ct);
            _logger.LogError("Falha na transcrição Azure OpenAI ({Status}): {Corpo}", (int)resposta.StatusCode, corpo);
            throw new InvalidOperationException("Não foi possível transcrever o áudio agora. Tente novamente ou digite a descrição.");
        }

        var resultado = await resposta.Content.ReadFromJsonAsync<RespostaTranscricao>(cancellationToken: ct);
        return resultado?.Text?.Trim() ?? string.Empty;
    }

    private sealed record RespostaTranscricao(string? Text);
}

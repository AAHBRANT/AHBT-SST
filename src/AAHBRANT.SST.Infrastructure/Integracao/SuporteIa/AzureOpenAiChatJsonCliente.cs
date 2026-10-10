using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Integracao.SuporteIa;

/// <summary>
/// Chamada de chat ao deployment gpt5mini do Azure OpenAI com saída estruturada (json_schema
/// strict). Compartilhada pelos classificadores de relato (Suporte IA e Ocorrências).
/// </summary>
public class AzureOpenAiChatJsonCliente
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<AzureOpenAiOptions> _options;
    private readonly ILogger<AzureOpenAiChatJsonCliente> _logger;

    public AzureOpenAiChatJsonCliente(
        IHttpClientFactory httpClientFactory,
        IOptions<AzureOpenAiOptions> options,
        ILogger<AzureOpenAiChatJsonCliente> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    // maxTokens/timeoutSegundos maiores para a leitura de PGR/PCMSO (10/10/2026); os classificadores de
    // relato continuam com 3000 tokens e 60 s. Limite de taxa (429) e erro do serviço (5xx) são tentados
    // de novo até 3 vezes — a leitura de um documento faz dezenas de chamadas seguidas.
    public async Task<T> ObterAsync<T>(string instrucoesSistema, string mensagemUsuario, string nomeSchema, object schema, CancellationToken ct,
        int maxTokens = 3000, int timeoutSegundos = 60)
    {
        var opcoes = _options.Value;
        if (string.IsNullOrWhiteSpace(opcoes.Endpoint) || string.IsNullOrWhiteSpace(opcoes.ApiKey))
            throw new InvalidOperationException("Classificação por IA não configurada. Configure AzureOpenAI:Endpoint e AzureOpenAI:ApiKey.");

        var url = $"{opcoes.Endpoint.TrimEnd('/')}/openai/deployments/{opcoes.DeploymentChat}/chat/completions?api-version={opcoes.ApiVersionChat}";
        var corpo = new
        {
            messages = new object[]
            {
                new { role = "system", content = instrucoesSistema },
                new { role = "user", content = mensagemUsuario },
            },
            max_completion_tokens = maxTokens,
            reasoning_effort = "low",
            response_format = new
            {
                type = "json_schema",
                json_schema = new { name = nomeSchema, strict = true, schema },
            },
        };

        var cliente = _httpClientFactory.CreateClient();
        cliente.Timeout = TimeSpan.FromSeconds(timeoutSegundos);

        HttpResponseMessage resposta;
        for (var tentativa = 1; ; tentativa++)
        {
            using var requisicao = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(corpo) };
            requisicao.Headers.Add("api-key", opcoes.ApiKey);
            resposta = await cliente.SendAsync(requisicao, ct);
            var temporario = (int)resposta.StatusCode == 429 || (int)resposta.StatusCode >= 500;
            if (!temporario || tentativa == 3) break;
            var espera = resposta.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5 * tentativa);
            _logger.LogWarning("Azure OpenAI respondeu {Status}; nova tentativa em {Espera}s", (int)resposta.StatusCode, espera.TotalSeconds);
            resposta.Dispose();
            await Task.Delay(espera, ct);
        }
        using var _ = resposta;

        if (!resposta.IsSuccessStatusCode)
        {
            var erro = await resposta.Content.ReadAsStringAsync(ct);
            _logger.LogError("Falha na chamada de chat Azure OpenAI ({Status}): {Corpo}", (int)resposta.StatusCode, erro);
            throw new InvalidOperationException("A IA não conseguiu analisar o relato agora.");
        }

        using var json = await JsonDocument.ParseAsync(await resposta.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var conteudo = json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()
            ?? throw new InvalidOperationException("A IA devolveu uma resposta vazia.");

        return JsonSerializer.Deserialize<T>(conteudo, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("A IA devolveu uma resposta inválida.");
    }

    // Corta no limite da coluna para a sugestão nunca ser recusada pelo validador do comando.
    public static string Limitar(string? texto, int maximo)
    {
        var limpo = (texto ?? string.Empty).Trim();
        return limpo.Length <= maximo ? limpo : limpo[..maximo].TrimEnd();
    }

    public static string? LimitarOuNulo(string? texto, int maximo)
        => string.IsNullOrWhiteSpace(texto) ? null : Limitar(texto, maximo);
}

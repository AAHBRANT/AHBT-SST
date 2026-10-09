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

    public async Task<T> ObterAsync<T>(string instrucoesSistema, string mensagemUsuario, string nomeSchema, object schema, CancellationToken ct)
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
            max_completion_tokens = 3000,
            reasoning_effort = "low",
            response_format = new
            {
                type = "json_schema",
                json_schema = new { name = nomeSchema, strict = true, schema },
            },
        };

        using var requisicao = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(corpo) };
        requisicao.Headers.Add("api-key", opcoes.ApiKey);

        var cliente = _httpClientFactory.CreateClient();
        cliente.Timeout = TimeSpan.FromSeconds(60);
        using var resposta = await cliente.SendAsync(requisicao, ct);

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

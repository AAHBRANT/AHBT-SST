using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Integracao.SuporteIa;

/// <summary>
/// Chamada de chat ao Claude (Anthropic) com saída estruturada (json_schema
/// strict). Compartilhada pelos classificadores de relato (Suporte IA e Ocorrências).
/// </summary>
public class ClaudeChatJsonCliente
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<AzureOpenAiOptions> _options;
    private readonly IOptions<ClaudeOptions> _claude;
    private readonly ILogger<ClaudeChatJsonCliente> _logger;

    public ClaudeChatJsonCliente(
        IHttpClientFactory httpClientFactory,
        IOptions<AzureOpenAiOptions> options,
        IOptions<ClaudeOptions> claude,
        ILogger<ClaudeChatJsonCliente> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _claude = claude;
        _logger = logger;
    }

    // maxTokens/timeoutSegundos maiores para a leitura de PGR/PCMSO (10/10/2026); os classificadores de
    // relato continuam com 3000 tokens e 60 s. Limite de taxa (429) e erro do serviço (5xx) são tentados
    // de novo até 3 vezes — a leitura de um documento faz dezenas de chamadas seguidas.
    // imagens (21/10/2026, OCR de PDF digitalizado): data URLs ("data:image/jpeg;base64,...") enviadas junto
    // com o texto na mesma mensagem do usuário, em alta resolução para a IA ler letra miúda.
    public async Task<T> ObterAsync<T>(string instrucoesSistema, string mensagemUsuario, string nomeSchema, object schema, CancellationToken ct,
        int maxTokens = 3000, int timeoutSegundos = 60, IReadOnlyList<string>? imagens = null)
    {
        // Regra (22/10/2026): IA só pelo Claude, chave já contratada. O caminho Azure abaixo fica desligado.
        if (string.IsNullOrWhiteSpace(_claude.Value.ApiKey))
            throw new InvalidOperationException("IA não configurada. Configure Claude:ApiKey.");
        return await ObterViaClaudeAsync<T>(instrucoesSistema, mensagemUsuario, schema, ct, maxTokens, timeoutSegundos, imagens);
    }

    // Desligado desde 22/10/2026 (troca para o Claude); mantido só como referência.
    private async Task<T> ObterViaAzureAsync<T>(string instrucoesSistema, string mensagemUsuario, string nomeSchema, object schema, CancellationToken ct,
        int maxTokens, int timeoutSegundos, IReadOnlyList<string>? imagens)
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
                new { role = "user", content = ConteudoUsuario(mensagemUsuario, imagens) },
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

    // Messages API do Claude com saída estruturada (output_config.format json_schema). Mesma política de
    // novas tentativas (429/5xx, até 3 vezes) do caminho Azure.
    private async Task<T> ObterViaClaudeAsync<T>(string instrucoesSistema, string mensagemUsuario, object schema, CancellationToken ct,
        int maxTokens, int timeoutSegundos, IReadOnlyList<string>? imagens)
    {
        var opcoes = _claude.Value;
        var (schemaEnviado, textosVaziosComoNulo) = ReduzirUnioes(schema);
        if (textosVaziosComoNulo.Count > 0)
            instrucoesSistema += "\n\nCampo de texto ausente ou ilegível: devolva string vazia (\"\").";
        var corpo = new
        {
            model = opcoes.Modelo,
            max_tokens = maxTokens,
            system = instrucoesSistema,
            messages = new object[] { new { role = "user", content = ConteudoUsuarioClaude(mensagemUsuario, imagens) } },
            output_config = new { format = new { type = "json_schema", schema = schemaEnviado } },
        };
        var url = $"{opcoes.Endpoint.TrimEnd('/')}/v1/messages";

        var cliente = _httpClientFactory.CreateClient();
        cliente.Timeout = TimeSpan.FromSeconds(timeoutSegundos);

        HttpResponseMessage resposta;
        for (var tentativa = 1; ; tentativa++)
        {
            using var requisicao = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(corpo) };
            requisicao.Headers.Add("x-api-key", opcoes.ApiKey);
            requisicao.Headers.Add("anthropic-version", "2023-06-01");
            if (!string.IsNullOrWhiteSpace(opcoes.WorkspaceId))
                requisicao.Headers.Add("anthropic-workspace-id", opcoes.WorkspaceId);
            resposta = await cliente.SendAsync(requisicao, ct);
            var temporario = (int)resposta.StatusCode == 429 || (int)resposta.StatusCode >= 500;
            if (!temporario || tentativa == 3) break;
            var espera = resposta.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5 * tentativa);
            _logger.LogWarning("Claude respondeu {Status}; nova tentativa em {Espera}s", (int)resposta.StatusCode, espera.TotalSeconds);
            resposta.Dispose();
            await Task.Delay(espera, ct);
        }
        using var _ = resposta;

        if (!resposta.IsSuccessStatusCode)
        {
            var erro = await resposta.Content.ReadAsStringAsync(ct);
            _logger.LogError("Falha na chamada ao Claude ({Status}): {Corpo}", (int)resposta.StatusCode, erro);
            throw new InvalidOperationException("A IA não conseguiu analisar o relato agora.");
        }

        using var json = await JsonDocument.ParseAsync(await resposta.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var conteudo = json.RootElement.GetProperty("content").EnumerateArray()
            .Where(b => b.GetProperty("type").GetString() == "text")
            .Select(b => b.GetProperty("text").GetString())
            .FirstOrDefault()
            ?? throw new InvalidOperationException("A IA devolveu uma resposta vazia.");

        if (textosVaziosComoNulo.Count > 0)
        {
            var raiz = JsonNode.Parse(conteudo);
            foreach (var caminho in textosVaziosComoNulo) VazioParaNulo(raiz, caminho, 0);
            conteudo = raiz?.ToJsonString() ?? conteudo;
        }

        return JsonSerializer.Deserialize<T>(conteudo, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("A IA devolveu uma resposta inválida.");
    }

    // O Claude recusa schema com mais de 16 campos "tipo ou null" (23/10/2026: leitura do contrato de
    // terceirizada, erro 400 "too many parameters with union types"). Acima do limite, os textos opcionais
    // viram string simples e o "" devolvido volta a null antes de desserializar — quem consome não muda.
    private const int LimiteUnioesClaude = 16;

    private static (JsonNode Schema, List<string[]> TextosVaziosComoNulo) ReduzirUnioes(object schema)
    {
        var raiz = JsonSerializer.SerializeToNode(schema)!;
        TipoUnicoSemLista(raiz);
        var caminhos = new List<string[]>();
        if (ContarUnioes(raiz) <= LimiteUnioesClaude) return (raiz, caminhos);

        void Visitar(JsonNode? no, List<string> caminho)
        {
            if (no is not JsonObject obj) return;
            if (obj["properties"] is JsonObject props)
                foreach (var (nome, filho) in props.ToList())
                {
                    var filhoCaminho = new List<string>(caminho) { nome };
                    if (filho is JsonObject f && f["type"] is JsonArray tipos && tipos.Count == 2
                        && tipos.Any(t => t?.GetValue<string>() == "string") && tipos.Any(t => t?.GetValue<string>() == "null"))
                    {
                        f["type"] = "string";
                        caminhos.Add(filhoCaminho.ToArray());
                    }
                    Visitar(filho, filhoCaminho);
                }
            if (obj["items"] is JsonObject itens) Visitar(itens, new List<string>(caminho) { "*" });
        }

        Visitar(raiz, new List<string>());
        return (raiz, caminhos);
    }

    // {"type": ["string"]} conta como união para o Claude; vira {"type": "string"} (mesmo significado).
    private static void TipoUnicoSemLista(JsonNode? no)
    {
        if (no is JsonObject obj)
        {
            if (obj["type"] is JsonArray tipos && tipos.Count == 1) obj["type"] = tipos[0]!.GetValue<string>();
            foreach (var p in obj.ToList()) TipoUnicoSemLista(p.Value);
        }
        else if (no is JsonArray arr)
            foreach (var item in arr.ToList()) TipoUnicoSemLista(item);
    }

    private static int ContarUnioes(JsonNode? no) => no switch
    {
        JsonObject obj => (obj["type"] is JsonArray || obj.ContainsKey("anyOf") ? 1 : 0)
            + obj.Sum(p => ContarUnioes(p.Value)),
        JsonArray arr => arr.Sum(ContarUnioes),
        _ => 0,
    };

    private static void VazioParaNulo(JsonNode? no, string[] caminho, int i)
    {
        if (i == caminho.Length - 1)
        {
            if (no is JsonObject obj && obj[caminho[i]] is JsonValue v && v.TryGetValue<string>(out var texto)
                && string.IsNullOrWhiteSpace(texto))
                obj[caminho[i]] = null;
            return;
        }
        if (caminho[i] == "*")
        {
            if (no is JsonArray arr) foreach (var item in arr) VazioParaNulo(item, caminho, i + 1);
            return;
        }
        if (no is JsonObject o) VazioParaNulo(o[caminho[i]], caminho, i + 1);
    }

    // Data URL "data:image/jpeg;base64,..." → bloco de imagem base64 do Claude.
    private static object ConteudoUsuarioClaude(string texto, IReadOnlyList<string>? imagens)
    {
        if (imagens is not { Count: > 0 }) return texto;
        var partes = new List<object>();
        foreach (var dataUrl in imagens)
        {
            var virgula = dataUrl.IndexOf(',');
            var cabecalho = dataUrl[..virgula];
            var tipo = cabecalho["data:".Length..].Split(';')[0];
            partes.Add(new { type = "image", source = new { type = "base64", media_type = tipo, data = dataUrl[(virgula + 1)..] } });
        }
        partes.Add(new { type = "text", text = texto });
        return partes;
    }

    private static object ConteudoUsuario(string texto, IReadOnlyList<string>? imagens)
    {
        if (imagens is not { Count: > 0 }) return texto;
        var partes = new List<object> { new { type = "text", text = texto } };
        partes.AddRange(imagens.Select(url => (object)new { type = "image_url", image_url = new { url, detail = "high" } }));
        return partes;
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

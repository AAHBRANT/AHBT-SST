using System.Net.Http.Json;
using System.Text.Json;
using AAHBRANT.SST.Application.SuporteIa;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Integracao.SuporteIa;

/// <summary>
/// Classificação do relato via deployment gpt5mini (gpt-5.4-mini) do Azure OpenAI, com saída
/// estruturada (json_schema strict) — o modelo só pode devolver valores válidos dos enums.
/// </summary>
public class AzureOpenAiClassificadorRelato : IClassificadorRelatoSuporteIa
{
    private const string InstrucoesSistema = """
        Você classifica relatos de usuários do sistema SST (Segurança e Saúde do Trabalho) da AAHBRANT
        para abrir um chamado de suporte. O relato normalmente foi falado e transcrito, então pode ter
        hesitações e repetições. Responda só com o JSON pedido.
        - tipo: Erro (algo quebrado ou com mensagem de erro), Duvida (como usar alguma coisa),
          Melhoria (pedido de mudança ou funcionalidade nova).
        - severidade: Baixa (incômodo pequeno), Media (atrapalha mas há alternativa), Alta (impede uma
          tarefa importante), Critica (para o trabalho em obra ou gera risco legal/de segurança).
        - titulo: até 80 caracteres, direto, como o usuário diria.
        - modulo: tela ou módulo citado (ex.: Inspeções, EPI, EPC, Uniforme, DDS, APR, PT,
          Treinamentos, Funcionários, Obras, Acidentes, CIPA, PGR) ou null se não der para saber.
        - descricao: o relato reescrito em texto claro e organizado, em português, sem inventar fatos
          e sem remover detalhes do que o usuário disse.
        """;

    private static readonly object FormatoResposta = new
    {
        type = "json_schema",
        json_schema = new
        {
            name = "chamado_suporte",
            strict = true,
            schema = new
            {
                type = "object",
                additionalProperties = false,
                required = new[] { "tipo", "severidade", "titulo", "modulo", "descricao" },
                properties = new
                {
                    tipo = new { type = "string", @enum = new[] { "Erro", "Duvida", "Melhoria" } },
                    severidade = new { type = "string", @enum = new[] { "Baixa", "Media", "Alta", "Critica" } },
                    titulo = new { type = "string" },
                    modulo = new { type = new[] { "string", "null" } },
                    descricao = new { type = "string" },
                },
            },
        },
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<AzureOpenAiOptions> _options;
    private readonly ILogger<AzureOpenAiClassificadorRelato> _logger;

    public AzureOpenAiClassificadorRelato(
        IHttpClientFactory httpClientFactory,
        IOptions<AzureOpenAiOptions> options,
        ILogger<AzureOpenAiClassificadorRelato> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<ChamadoSugeridoSuporteIa> ClassificarAsync(string relato, CancellationToken ct)
    {
        var opcoes = _options.Value;
        if (string.IsNullOrWhiteSpace(opcoes.Endpoint) || string.IsNullOrWhiteSpace(opcoes.ApiKey))
            throw new InvalidOperationException("Classificação por IA não configurada. Configure AzureOpenAI:Endpoint e AzureOpenAI:ApiKey.");

        var url = $"{opcoes.Endpoint.TrimEnd('/')}/openai/deployments/{opcoes.DeploymentChat}/chat/completions?api-version={opcoes.ApiVersionChat}";
        var corpo = new
        {
            messages = new object[]
            {
                new { role = "system", content = InstrucoesSistema },
                new { role = "user", content = relato },
            },
            max_completion_tokens = 2000,
            reasoning_effort = "low",
            response_format = FormatoResposta,
        };

        using var requisicao = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(corpo) };
        requisicao.Headers.Add("api-key", opcoes.ApiKey);

        var cliente = _httpClientFactory.CreateClient();
        cliente.Timeout = TimeSpan.FromSeconds(60);
        using var resposta = await cliente.SendAsync(requisicao, ct);

        if (!resposta.IsSuccessStatusCode)
        {
            var erro = await resposta.Content.ReadAsStringAsync(ct);
            _logger.LogError("Falha na classificação Azure OpenAI ({Status}): {Corpo}", (int)resposta.StatusCode, erro);
            throw new InvalidOperationException("A IA não conseguiu classificar o relato agora.");
        }

        using var json = await JsonDocument.ParseAsync(await resposta.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var conteudo = json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()
            ?? throw new InvalidOperationException("A IA devolveu uma resposta vazia.");

        var sugestao = JsonSerializer.Deserialize<RespostaModelo>(conteudo, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("A IA devolveu uma resposta inválida.");

        return new ChamadoSugeridoSuporteIa(
            Enum.TryParse<TipoSolicitacaoSuporteIa>(sugestao.Tipo, out var tipo) ? tipo : TipoSolicitacaoSuporteIa.Duvida,
            Enum.TryParse<SeveridadeSolicitacaoSuporteIa>(sugestao.Severidade, out var severidade) ? severidade : SeveridadeSolicitacaoSuporteIa.Media,
            Limitar(sugestao.Titulo, 180),
            string.IsNullOrWhiteSpace(sugestao.Modulo) ? null : Limitar(sugestao.Modulo, 120),
            Limitar(sugestao.Descricao, 4000));
    }

    // Mesmos limites do CriarSolicitacaoSuporteIaCommandValidator, para a sugestão nunca ser recusada.
    private static string Limitar(string? texto, int maximo)
    {
        var limpo = (texto ?? string.Empty).Trim();
        return limpo.Length <= maximo ? limpo : limpo[..maximo].TrimEnd();
    }

    private sealed record RespostaModelo(string? Tipo, string? Severidade, string? Titulo, string? Modulo, string? Descricao);
}

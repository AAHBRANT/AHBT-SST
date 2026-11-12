using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using AAHBRANT.SST.AgenteCameras.Envio;
using AAHBRANT.SST.AgenteCameras.Seguranca;

namespace AAHBRANT.SST.AgenteCameras.Servidor;

public enum TipoConsulta
{
    Sucesso,          // 200
    SegredoInvalido,  // 401: segredo inválido ou agente revogado
    LimiteTaxa,       // 429
    Indisponivel,     // 5xx, 404, outros 4xx, rede, timeout, resposta ilegível: mantém a última configuração
}

public sealed record ResultadoConsulta(TipoConsulta Tipo, int? StatusHttp, ConfiguracaoServidor? Configuracao, TimeSpan? EsperarPor, string? Detalhe)
{
    public static ResultadoConsulta Indisp(string detalhe, int? status = null) => new(TipoConsulta.Indisponivel, status, null, null, detalhe);
}

public enum TipoEnvioTeste
{
    Enviado,             // 2xx
    TesteNaoEncontrado,  // 404 com codigo teste_nao_encontrado: expirou ou não é deste agente; ignorar
    SegredoInvalido,     // 401
    Descartado,          // 400 e demais 4xx: repetir não adianta
    Falha,               // 5xx, 429, rede, timeout: pode tentar de novo (o servidor mantém o primeiro resultado)
}

public interface IClienteConfiguracao
{
    Task<ResultadoConsulta> ConsultarAsync(CancellationToken ct);
    Task<TipoEnvioTeste> EnviarResultadoTesteAsync(Guid testeId, ResultadoTesteConexao resultado, CancellationToken ct);
}

// GET  {ApiBaseUrl}/api/agentes-camera/{agenteId}/configuracao                       (sinal de vida do agente)
// POST {ApiBaseUrl}/api/agentes-camera/{agenteId}/testes-conexao/{testeId}/resultado
// Mesmos cabeçalhos (X-Agente-Segredo, X-Agente-Versao) e mesmo HttpClient do ClienteQuadros.
public sealed class ClienteConfiguracao : IClienteConfiguracao
{
    public static readonly TimeSpan TempoLimiteConsulta = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan TempoLimiteResultado = TimeSpan.FromSeconds(20);
    public const string CodigoTesteNaoEncontrado = "teste_nao_encontrado";
    private static readonly TimeSpan RetryAfterMaximo = TimeSpan.FromMinutes(10);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly HttpClient _http;
    private readonly Uri _baseUrl;
    private readonly string _agenteId;
    private readonly string _segredo;
    private readonly Mascarador _mascarador;
    private readonly TimeSpan _limiteConsulta;
    private readonly TimeSpan _limiteResultado;

    public ClienteConfiguracao(HttpClient http, Uri baseUrl, string agenteId, string segredo, Mascarador mascarador,
        TimeSpan? limiteConsulta = null, TimeSpan? limiteResultado = null)
    {
        _http = http;
        _baseUrl = ClienteQuadros.NormalizarBase(baseUrl);
        _agenteId = agenteId;
        _segredo = segredo;
        _mascarador = mascarador;
        _limiteConsulta = limiteConsulta ?? TempoLimiteConsulta;
        _limiteResultado = limiteResultado ?? TempoLimiteResultado;
    }

    private Uri Rota(string resto) => new(_baseUrl, $"api/agentes-camera/{Uri.EscapeDataString(_agenteId)}/{resto}");

    private HttpRequestMessage Requisicao(HttpMethod metodo, Uri url)
    {
        var r = new HttpRequestMessage(metodo, url);
        r.Headers.Add("X-Agente-Segredo", _segredo);
        r.Headers.Add("X-Agente-Versao", ClienteQuadros.VersaoDoAgente);
        return r;
    }

    public async Task<ResultadoConsulta> ConsultarAsync(CancellationToken ct)
    {
        using var requisicao = Requisicao(HttpMethod.Get, Rota("configuracao"));
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(_limiteConsulta);
        try
        {
            using var resposta = await _http.SendAsync(requisicao, limite.Token).ConfigureAwait(false);
            var status = (int)resposta.StatusCode;
            switch (resposta.StatusCode)
            {
                case HttpStatusCode.OK:
                    try
                    {
                        var texto = await resposta.Content.ReadAsStringAsync(limite.Token).ConfigureAwait(false);
                        var config = Desserializar(texto);
                        return config is null
                            ? ResultadoConsulta.Indisp("resposta vazia da API", status)
                            : new ResultadoConsulta(TipoConsulta.Sucesso, status, config, null, null);
                    }
                    catch (JsonException)
                    {
                        return ResultadoConsulta.Indisp("resposta da API em formato inesperado", status);
                    }
                case HttpStatusCode.Unauthorized:
                    return new ResultadoConsulta(TipoConsulta.SegredoInvalido, status, null, null, "segredo inválido ou agente revogado");
                case HttpStatusCode.TooManyRequests:
                    return new ResultadoConsulta(TipoConsulta.LimiteTaxa, status, null, LerRetryAfter(resposta), "limite de requisições");
            }
            return ResultadoConsulta.Indisp(status >= 500 ? $"erro do servidor (HTTP {status})" : $"HTTP {status} inesperado (rota ou URL da API incorreta, ou API em atualização)", status);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return ResultadoConsulta.Indisp($"tempo esgotado ({_limiteConsulta.TotalSeconds:0} s) sem resposta da API");
        }
        catch (HttpRequestException e)
        {
            return ResultadoConsulta.Indisp(_mascarador.Mascarar(e.Message));
        }
    }

    public async Task<TipoEnvioTeste> EnviarResultadoTesteAsync(Guid testeId, ResultadoTesteConexao resultado, CancellationToken ct)
    {
        using var requisicao = Requisicao(HttpMethod.Post, Rota($"testes-conexao/{testeId:D}/resultado"));
        requisicao.Content = new StringContent(Serializar(resultado), System.Text.Encoding.UTF8, new MediaTypeHeaderValue("application/json"));
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(_limiteResultado);
        try
        {
            using var resposta = await _http.SendAsync(requisicao, limite.Token).ConfigureAwait(false);
            if ((int)resposta.StatusCode is >= 200 and < 300) return TipoEnvioTeste.Enviado;
            switch (resposta.StatusCode)
            {
                case HttpStatusCode.Unauthorized:
                    return TipoEnvioTeste.SegredoInvalido;
                case HttpStatusCode.NotFound:
                    // Só o 404 "de negócio" (código no corpo) quer dizer "teste inexistente/expirado".
                    return await CorpoTemCodigoAsync(resposta, CodigoTesteNaoEncontrado, limite.Token).ConfigureAwait(false)
                        ? TipoEnvioTeste.TesteNaoEncontrado
                        : TipoEnvioTeste.Falha;
                case HttpStatusCode.TooManyRequests:
                case HttpStatusCode.RequestTimeout:
                case HttpStatusCode.Conflict:
                    return TipoEnvioTeste.Falha;
            }
            return (int)resposta.StatusCode >= 500 ? TipoEnvioTeste.Falha : TipoEnvioTeste.Descartado;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return TipoEnvioTeste.Falha;
        }
        catch (HttpRequestException)
        {
            return TipoEnvioTeste.Falha;
        }
    }

    private static async Task<bool> CorpoTemCodigoAsync(HttpResponseMessage r, string codigo, CancellationToken ct)
    {
        try
        {
            var texto = await r.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(texto)) return false;
            using var doc = JsonDocument.Parse(texto);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;
            foreach (var p in doc.RootElement.EnumerateObject())
                if (p.Name.Equals("codigo", StringComparison.OrdinalIgnoreCase)
                    && p.Value.ValueKind == JsonValueKind.String
                    && string.Equals(p.Value.GetString(), codigo, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static TimeSpan? LerRetryAfter(HttpResponseMessage r)
    {
        var h = r.Headers.RetryAfter;
        TimeSpan? espera = null;
        if (h?.Delta is { } delta) espera = delta;
        else if (h?.Date is { } data) espera = data - DateTimeOffset.UtcNow;
        if (espera is null) return null;
        if (espera.Value < TimeSpan.FromSeconds(1)) return TimeSpan.FromSeconds(1);
        return espera.Value > RetryAfterMaximo ? RetryAfterMaximo : espera;
    }

    internal static ConfiguracaoServidor? Desserializar(string json) => JsonSerializer.Deserialize<ConfiguracaoServidor>(json, Json);
    internal static string Serializar(ResultadoTesteConexao r) => JsonSerializer.Serialize(r, Json);
}

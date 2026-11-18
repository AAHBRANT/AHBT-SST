using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using AAHBRANT.SST.AgenteCameras.Seguranca;

namespace AAHBRANT.SST.AgenteCameras.Envio;

public enum TipoResultado
{
    Sucesso,          // 200
    SegredoInvalido,  // 401: segredo inválido ou agente revogado
    CameraNaoPertence,// 404 COM corpo {"codigo":"camera_nao_encontrada"}: câmera não pertence à obra do agente
    LimiteTaxa,       // 429
    Descartado,       // 400, 413 (e demais 4xx): repetir não adianta
    SemInternet,      // 5xx, 409, 404 sem o código, rede, timeout: indisponibilidade; tenta de novo
}

public sealed record RespostaQuadro
{
    public string? Situacao { get; init; }
    public Guid? OcorrenciaId { get; init; }
    public bool? AlertaEnviado { get; init; }
    public int? Pessoas { get; init; }
    public int? SemCapacete { get; init; }
    public int? SemColete { get; init; }
    public Guid? TrabalhadorId { get; init; }
    public string? TrabalhadorNome { get; init; }
    public bool? Repetida { get; init; }
}

public sealed record ResultadoEnvio(TipoResultado Tipo, int? StatusHttp, RespostaQuadro? Resposta, TimeSpan? EsperarPor, string? Detalhe)
{
    public static ResultadoEnvio SemInternet(string detalhe, int? status = null) => new(TipoResultado.SemInternet, status, null, null, detalhe);
}

public interface IClienteQuadros
{
    Task<ResultadoEnvio> EnviarAsync(string cameraId, byte[] jpeg, DateTimeOffset capturadaEm, CancellationToken ct);
}

// POST {ApiBaseUrl}/api/agentes-camera/{agenteId}/cameras/{cameraId}/quadros
//   X-Agente-Segredo: <segredo>; multipart/form-data com Foto (image/jpeg, quadro.jpg) e CapturadaEm (ISO 8601 com offset).
public sealed class ClienteQuadros : IClienteQuadros
{
    public static readonly TimeSpan TempoLimite = TimeSpan.FromSeconds(90);

    /// <summary>Código que a API devolve no corpo do 404 quando a câmera não pertence a este agente.</summary>
    public const string CodigoCameraNaoEncontrada = "camera_nao_encontrada";

    public static readonly string VersaoDoAgente =
        typeof(ClienteQuadros).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    private static readonly TimeSpan RetryAfterMaximo = TimeSpan.FromMinutes(10);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private readonly HttpClient _http;
    private readonly Uri _baseUrl;
    private readonly string _agenteId;
    private readonly string _segredo;
    private readonly Mascarador _mascarador;
    private readonly TimeSpan _tempoLimite;

    // O HttpClient deve ter Timeout >= TempoLimite (o limite de 90 s é aplicado aqui também, por requisição).
    public ClienteQuadros(HttpClient http, Uri baseUrl, string agenteId, string segredo, Mascarador mascarador, TimeSpan? tempoLimite = null)
    {
        _tempoLimite = tempoLimite ?? TempoLimite;
        _http = http;
        _baseUrl = NormalizarBase(baseUrl);
        _agenteId = agenteId;
        _segredo = segredo;
        _mascarador = mascarador;
    }

    // Garante a barra final: sem ela, new Uri(base, "api/...") descarta o último segmento do caminho
    // (https://host/sst + api/x vira https://host/api/x). Query e fragmento da base não fazem sentido aqui.
    internal static Uri NormalizarBase(Uri baseUrl)
    {
        var b = new UriBuilder(baseUrl) { Query = "", Fragment = "" };
        if (!b.Path.EndsWith('/')) b.Path += "/";
        return b.Uri;
    }

    public async Task<ResultadoEnvio> EnviarAsync(string cameraId, byte[] jpeg, DateTimeOffset capturadaEm, CancellationToken ct)
    {
        var url = new Uri(_baseUrl, $"api/agentes-camera/{Uri.EscapeDataString(_agenteId)}/cameras/{Uri.EscapeDataString(cameraId)}/quadros");
        using var corpo = new MultipartFormDataContent();
        var foto = new ByteArrayContent(jpeg);
        foto.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        corpo.Add(foto, "Foto", "quadro.jpg");
        corpo.Add(new StringContent(capturadaEm.ToString("o", System.Globalization.CultureInfo.InvariantCulture)), "CapturadaEm");

        using var requisicao = new HttpRequestMessage(HttpMethod.Post, url) { Content = corpo };
        requisicao.Headers.Add("X-Agente-Segredo", _segredo);
        // Versão deste agente: o servidor mostra na lista de agentes (campo Versao).
        requisicao.Headers.Add("X-Agente-Versao", VersaoDoAgente);

        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(_tempoLimite);
        try
        {
            using var resposta = await _http.SendAsync(requisicao, limite.Token).ConfigureAwait(false);
            return await InterpretarAsync(resposta, limite.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return ResultadoEnvio.SemInternet($"tempo esgotado ({_tempoLimite.TotalSeconds:0} s) sem resposta da API");
        }
        catch (HttpRequestException e)
        {
            return ResultadoEnvio.SemInternet(_mascarador.Mascarar(e.Message));
        }
    }

    private static async Task<ResultadoEnvio> InterpretarAsync(HttpResponseMessage r, CancellationToken ct)
    {
        var status = (int)r.StatusCode;
        switch (r.StatusCode)
        {
            case HttpStatusCode.OK:
                RespostaQuadro? dados = null;
                try
                {
                    dados = await r.Content.ReadFromJsonAsyncSeguro(ct).ConfigureAwait(false);
                }
                catch (Exception e) when (e is JsonException or NotSupportedException)
                {
                    // 200 sem JSON legível: o quadro foi aceito; só não há resumo para o log.
                }
                return new ResultadoEnvio(TipoResultado.Sucesso, status, dados, null, null);
            case HttpStatusCode.Unauthorized:
                return new ResultadoEnvio(TipoResultado.SegredoInvalido, status, null, null, "segredo inválido ou agente revogado");
            case HttpStatusCode.NotFound:
                // Só o 404 "de negócio" (com o código no corpo) significa câmera de outra obra. Qualquer outro 404
                // (rota inexistente, ingress durante um deploy, URL base errada) é indisponibilidade: o quadro
                // fica na fila e a tentativa se repete.
                if (await CorpoTemCodigoAsync(r, CodigoCameraNaoEncontrada, ct).ConfigureAwait(false))
                    return new ResultadoEnvio(TipoResultado.CameraNaoPertence, status, null, null, "câmera não pertence à obra do agente");
                return ResultadoEnvio.SemInternet($"HTTP 404 sem o código {CodigoCameraNaoEncontrada} (rota ou URL da API incorreta, ou API em atualização)", status);
            case HttpStatusCode.Conflict:
                return ResultadoEnvio.SemInternet("conflito no servidor (HTTP 409); nova tentativa", status);
            case HttpStatusCode.TooManyRequests:
                return new ResultadoEnvio(TipoResultado.LimiteTaxa, status, null, LerRetryAfter(r), "limite de requisições");
            case HttpStatusCode.RequestTimeout:
                return ResultadoEnvio.SemInternet("servidor encerrou a requisição por tempo", status);
        }

        if (status >= 500) return ResultadoEnvio.SemInternet($"erro do servidor (HTTP {status})", status);
        return new ResultadoEnvio(TipoResultado.Descartado, status, null, null, $"quadro recusado (HTTP {status})");
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

    internal static RespostaQuadro? Desserializar(string json) => JsonSerializer.Deserialize<RespostaQuadro>(json, Json);
}

internal static class ConteudoHttpExtensoes
{
    public static async Task<RespostaQuadro?> ReadFromJsonAsyncSeguro(this HttpContent conteudo, CancellationToken ct)
    {
        var texto = await conteudo.ReadAsStringAsync(ct).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(texto) ? null : ClienteQuadros.Desserializar(texto);
    }
}

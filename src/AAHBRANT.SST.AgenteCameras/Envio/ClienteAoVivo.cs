using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AAHBRANT.SST.AgenteCameras.Seguranca;

namespace AAHBRANT.SST.AgenteCameras.Envio;

public enum TipoResultadoAoVivo
{
    Continuar,         // 204: quadro aceito, seguir transmitindo
    Encerrada,         // 410 (sessao_encerrada) ou 404 COM codigo sessao_nao_encontrada: parar já
    SegredoInvalido,   // 401: segredo inválido ou agente revogado (não conta como "servidor respondeu")
    LimiteTaxa,        // 429: esperar e seguir
    Descartado,        // 400, 413 e demais 4xx: este quadro não serve; seguir com o próximo
    SemContato,        // 5xx, 409, 404 sem o código, rede, timeout: servidor indisponível
}

public sealed record ResultadoQuadroAoVivo(TipoResultadoAoVivo Tipo, int? StatusHttp, TimeSpan? EsperarPor, string? Detalhe)
{
    public static ResultadoQuadroAoVivo SemContato(string detalhe, int? status = null) => new(TipoResultadoAoVivo.SemContato, status, null, detalhe);
}

public interface IClienteAoVivo
{
    Task<ResultadoQuadroAoVivo> EnviarQuadroAsync(Guid sessaoId, byte[] jpeg, DateTimeOffset capturadoEm, CancellationToken ct);

    /// <summary>Avisa a API de que a câmera não abriu / a leitura caiu. Devolve true se a API aceitou (2xx).</summary>
    Task<bool> EnviarFalhaAsync(Guid sessaoId, string codigo, string motivo, CancellationToken ct);
}

// POST {ApiBaseUrl}/api/agentes-camera/{agenteId}/ao-vivo/{sessaoId}/quadros   (corpo image/jpeg cru; X-Capturado-Em opcional)
// POST {ApiBaseUrl}/api/agentes-camera/{agenteId}/ao-vivo/{sessaoId}/falha     ({ codigo, motivo })
// Mesmos cabeçalhos das outras rotas do agente (X-Agente-Segredo, X-Agente-Versao). Sem retentativa e sem fila: o
// chamador só tem interesse no quadro mais recente.
public sealed class ClienteAoVivo : IClienteAoVivo
{
    public static readonly TimeSpan TempoLimiteQuadro = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan TempoLimiteFalha = TimeSpan.FromSeconds(10);
    public const string CodigoSessaoNaoEncontrada = "sessao_nao_encontrada";
    private static readonly TimeSpan RetryAfterMaximo = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly Uri _baseUrl;
    private readonly string _agenteId;
    private readonly string _segredo;
    private readonly Mascarador _mascarador;
    private readonly TimeSpan _limiteQuadro;
    private readonly TimeSpan _limiteFalha;

    public ClienteAoVivo(HttpClient http, Uri baseUrl, string agenteId, string segredo, Mascarador mascarador,
        TimeSpan? limiteQuadro = null, TimeSpan? limiteFalha = null)
    {
        _http = http;
        _baseUrl = ClienteQuadros.NormalizarBase(baseUrl);
        _agenteId = agenteId;
        _segredo = segredo;
        _mascarador = mascarador;
        _limiteQuadro = limiteQuadro ?? TempoLimiteQuadro;
        _limiteFalha = limiteFalha ?? TempoLimiteFalha;
    }

    private Uri Rota(Guid sessaoId, string final) =>
        new(_baseUrl, $"api/agentes-camera/{Uri.EscapeDataString(_agenteId)}/ao-vivo/{sessaoId:D}/{final}");

    private HttpRequestMessage Requisicao(Uri url)
    {
        var r = new HttpRequestMessage(HttpMethod.Post, url);
        r.Headers.Add("X-Agente-Segredo", _segredo);
        r.Headers.Add("X-Agente-Versao", ClienteQuadros.VersaoDoAgente);
        return r;
    }

    public async Task<ResultadoQuadroAoVivo> EnviarQuadroAsync(Guid sessaoId, byte[] jpeg, DateTimeOffset capturadoEm, CancellationToken ct)
    {
        using var requisicao = Requisicao(Rota(sessaoId, "quadros"));
        var corpo = new ByteArrayContent(jpeg);
        corpo.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        requisicao.Content = corpo;
        requisicao.Headers.Add("X-Capturado-Em", capturadoEm.UtcDateTime.ToString("o", CultureInfo.InvariantCulture));

        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(_limiteQuadro);
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
            return ResultadoQuadroAoVivo.SemContato($"tempo esgotado ({_limiteQuadro.TotalSeconds:0} s) sem resposta da API");
        }
        catch (HttpRequestException e)
        {
            return ResultadoQuadroAoVivo.SemContato(_mascarador.Mascarar(e.Message));
        }
    }

    public async Task<bool> EnviarFalhaAsync(Guid sessaoId, string codigo, string motivo, CancellationToken ct)
    {
        using var requisicao = Requisicao(Rota(sessaoId, "falha"));
        requisicao.Content = new StringContent(JsonSerializer.Serialize(new { codigo, motivo }, Json),
            System.Text.Encoding.UTF8, new MediaTypeHeaderValue("application/json"));
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(_limiteFalha);
        try
        {
            using var resposta = await _http.SendAsync(requisicao, limite.Token).ConfigureAwait(false);
            return (int)resposta.StatusCode is >= 200 and < 300;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    private static async Task<ResultadoQuadroAoVivo> InterpretarAsync(HttpResponseMessage r, CancellationToken ct)
    {
        var status = (int)r.StatusCode;
        if (status is >= 200 and < 300)
            return new ResultadoQuadroAoVivo(TipoResultadoAoVivo.Continuar, status, null, null);

        switch (r.StatusCode)
        {
            case HttpStatusCode.Gone:
                return new ResultadoQuadroAoVivo(TipoResultadoAoVivo.Encerrada, status, null, "sessão encerrada pelo servidor");
            case HttpStatusCode.Unauthorized:
                return new ResultadoQuadroAoVivo(TipoResultadoAoVivo.SegredoInvalido, status, null, "segredo inválido ou agente revogado");
            case HttpStatusCode.NotFound:
                // Só o 404 "de negócio" (com o código no corpo) quer dizer "a sessão não existe". Outro 404 (rota
                // inexistente, deploy em andamento, URL errada) é indisponibilidade.
                if (await CorpoTemCodigoAsync(r, CodigoSessaoNaoEncontrada, ct).ConfigureAwait(false))
                    return new ResultadoQuadroAoVivo(TipoResultadoAoVivo.Encerrada, status, null, "sessão não encontrada no servidor");
                return ResultadoQuadroAoVivo.SemContato($"HTTP 404 sem o código {CodigoSessaoNaoEncontrada} (rota ou URL da API incorreta, ou API em atualização)", status);
            case HttpStatusCode.TooManyRequests:
                return new ResultadoQuadroAoVivo(TipoResultadoAoVivo.LimiteTaxa, status, LerRetryAfter(r), "limite de requisições");
            case HttpStatusCode.Conflict:
            case HttpStatusCode.RequestTimeout:
                return ResultadoQuadroAoVivo.SemContato($"servidor indisponível (HTTP {status})", status);
        }

        if (status >= 500) return ResultadoQuadroAoVivo.SemContato($"erro do servidor (HTTP {status})", status);
        return new ResultadoQuadroAoVivo(TipoResultadoAoVivo.Descartado, status, null, $"quadro recusado (HTTP {status})");
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
}

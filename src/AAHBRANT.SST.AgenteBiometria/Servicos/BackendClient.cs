using System.Net.Http.Json;
using AAHBRANT.SST.AgenteBiometria.Opcoes;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.AgenteBiometria.Servicos;

public record TemplateSincronizadoResponse(Guid TrabalhadorId, string TrabalhadorNome, string TemplateCriptografado);

public class BackendClient
{
    private readonly HttpClient _httpClient;
    private readonly AgenteOptions _options;

    public BackendClient(HttpClient httpClient, IOptions<AgenteOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<List<TemplateSincronizadoResponse>> SincronizarTemplatesAsync(CancellationToken ct)
    {
        var url = $"{_options.BackendBaseUrl}/api/dispositivos-agente/{_options.DispositivoId}/templates/sincronizar";
        var resposta = await _httpClient.PostAsJsonAsync(url, new { SegredoDispositivo = _options.SegredoDispositivo }, ct);
        resposta.EnsureSuccessStatusCode();
        return await resposta.Content.ReadFromJsonAsync<List<TemplateSincronizadoResponse>>(cancellationToken: ct) ?? new();
    }
}

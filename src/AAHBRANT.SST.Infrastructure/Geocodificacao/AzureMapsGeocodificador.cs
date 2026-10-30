using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using AAHBRANT.SST.Application.Common.Interfaces;
using Azure.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Geocodificacao;

// AzureMaps:ClientId = "Unique ID" (client id) da conta Azure Maps. Sem ele o recurso fica desligado.
// Acesso pela identidade gerenciada da API (papel "Azure Maps Search and Render Data Reader"), sem chave.
public class OpcoesAzureMaps
{
    public const string Secao = "AzureMaps";
    public string? ClientId { get; set; }
    public string Endpoint { get; set; } = "https://atlas.microsoft.com";
    public int TempoLimiteSegundos { get; set; } = 4;
}

public class GeocodificadorDesligado : IGeocodificadorReverso
{
    public Task<IReadOnlyList<string>> LinhasAsync(double latitude, double longitude, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<string>>([]);
}

public class AzureMapsGeocodificador(
    IHttpClientFactory fabrica, TokenCredential credencial, IMemoryCache cache,
    IOptions<OpcoesAzureMaps> opcoes, ILogger<AzureMapsGeocodificador> log) : IGeocodificadorReverso
{
    public const string NomeCliente = "azure-maps";
    private static readonly TimeSpan ValidadeAcerto = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ValidadeFalha = TimeSpan.FromMinutes(2);

    public async Task<IReadOnlyList<string>> LinhasAsync(double latitude, double longitude, CancellationToken ct)
    {
        var o = opcoes.Value;
        if (string.IsNullOrWhiteSpace(o.ClientId)) return [];
        // 4 casas decimais (~11 m): fotos tiradas no mesmo ponto reaproveitam a mesma consulta.
        var chave = string.Create(CultureInfo.InvariantCulture, $"geo:{latitude:F4}:{longitude:F4}");
        if (cache.TryGetValue(chave, out IReadOnlyList<string>? guardado) && guardado is not null) return guardado;
        try
        {
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limite.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(o.TempoLimiteSegundos, 1, 15)));
            var token = await credencial.GetTokenAsync(new TokenRequestContext(["https://atlas.microsoft.com/.default"]), limite.Token);
            var url = string.Create(CultureInfo.InvariantCulture,
                $"{o.Endpoint.TrimEnd('/')}/reverseGeocode?api-version=2025-01-01&coordinates={longitude:F6},{latitude:F6}&language=pt-BR");
            using var pedido = new HttpRequestMessage(HttpMethod.Get, url);
            pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            pedido.Headers.Add("x-ms-client-id", o.ClientId);
            using var resposta = await fabrica.CreateClient(NomeCliente).SendAsync(pedido, limite.Token);
            if (!resposta.IsSuccessStatusCode)
            {
                log.LogWarning("Azure Maps respondeu {Status} na geocodificação reversa.", (int)resposta.StatusCode);
                cache.Set(chave, (IReadOnlyList<string>)[], ValidadeFalha);
                return [];
            }
            using var doc = await JsonDocument.ParseAsync(await resposta.Content.ReadAsStreamAsync(limite.Token), cancellationToken: limite.Token);
            var linhas = ExtrairLinhas(doc.RootElement);
            cache.Set(chave, linhas, linhas.Count > 0 ? ValidadeAcerto : ValidadeFalha);
            return linhas;
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            log.LogWarning(e, "Falha na geocodificação reversa (Azure Maps).");
            return [];
        }
    }

    // Monta as linhas do carimbo, no padrão do Timestamp Camera: rua e número / bairro / cidade UF / CEP / país.
    // Lê só o que existe; resposta fora do esperado devolve vazio (nunca inventa).
    public static IReadOnlyList<string> ExtrairLinhas(JsonElement raiz)
    {
        if (raiz.ValueKind != JsonValueKind.Object || !raiz.TryGetProperty("features", out var feicoes)
            || feicoes.ValueKind != JsonValueKind.Array || feicoes.GetArrayLength() == 0) return [];
        var f = feicoes[0];
        if (f.ValueKind != JsonValueKind.Object || !f.TryGetProperty("properties", out var props) || props.ValueKind != JsonValueKind.Object
            || !props.TryGetProperty("address", out var a) || a.ValueKind != JsonValueKind.Object) return [];

        static string? Texto(JsonElement e, string nome) =>
            e.ValueKind == JsonValueKind.Object && e.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(v.GetString()) ? v.GetString()!.Trim() : null;

        var rua = Texto(a, "addressLine");
        var bairro = Texto(a, "neighborhood");
        var cidade = Texto(a, "locality");
        string? uf = null;
        if (a.TryGetProperty("adminDistricts", out var distritos) && distritos.ValueKind == JsonValueKind.Array && distritos.GetArrayLength() > 0)
            uf = Texto(distritos[0], "shortName") ?? Texto(distritos[0], "name");
        var cep = Texto(a, "postalCode");
        var pais = a.TryGetProperty("countryRegion", out var p) ? Texto(p, "name") : null;

        var linhas = new List<string>();
        void Adicionar(string? s) { if (!string.IsNullOrWhiteSpace(s)) linhas.Add(s); }
        Adicionar(rua);
        Adicionar(bairro);
        Adicionar(string.Join(' ', new[] { cidade, uf }.Where(s => !string.IsNullOrWhiteSpace(s))));
        Adicionar(cep);
        Adicionar(pais);
        // Sem nenhum campo estruturado, aceita o endereço já formatado.
        if (linhas.Count == 0) Adicionar(Texto(a, "formattedAddress"));
        return linhas;
    }
}

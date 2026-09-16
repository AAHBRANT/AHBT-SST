using System.Net.Http.Headers;
using System.Net.Http.Json;
using AAHBRANT.SST.Application.Common.Interfaces;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Integracao.Grh;

// Cliente REST do endpoint de carga inicial de Alojamento do G-RH — mesmo padrão de
// ColaboradorGrhClient: GET {BaseUrl}/api/integracoes/sst/alojamentos, array JSON puro, autenticação
// client-credentials reaproveitando as mesmas GrhOptions (mesmo tenant/App Registration/Scope já
// usados para Colaborador — o App Role precisa também liberar este novo recurso, ver pedido em
// docs/superpowers/2026-09-15-pedido-integracao-alojamento-grh.md).
public class AlojamentoGrhClient : IAlojamentoGrhClient
{
    private readonly GrhOptions _opcoes;
    private readonly IHttpClientFactory _httpClientFactory;

    public AlojamentoGrhClient(IOptions<GrhOptions> opcoes, IHttpClientFactory httpClientFactory)
    {
        _opcoes = opcoes.Value;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IReadOnlyList<AlojamentoGrhDto>> ListarTodosAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opcoes.ClientSecret))
            throw new InvalidOperationException(
                "Grh:ClientSecret não configurado — integração com o G-RH ainda não provisionada.");

        var credential = new ClientSecretCredential(_opcoes.TenantId, _opcoes.ClientId, _opcoes.ClientSecret);
        var token = await credential.GetTokenAsync(new TokenRequestContext(new[] { _opcoes.Scope }), ct);

        var httpClient = _httpClientFactory.CreateClient();
        using var requisicao = new HttpRequestMessage(
            HttpMethod.Get, $"{_opcoes.BaseUrl.TrimEnd('/')}/api/integracoes/sst/alojamentos");
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        var resposta = await httpClient.SendAsync(requisicao, ct);
        if (!resposta.IsSuccessStatusCode)
        {
            var corpoResposta = await resposta.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Falha ao consultar alojamentos no G-RH: {(int)resposta.StatusCode} {resposta.StatusCode} — {corpoResposta}");
        }

        var payload = await resposta.Content.ReadFromJsonAsync<List<AlojamentoGrhPayload>>(cancellationToken: ct)
            ?? new List<AlojamentoGrhPayload>();

        return payload.ConvertAll(AlojamentoGrhPayloadMapper.Mapear);
    }
}

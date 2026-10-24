using System.Net.Http.Headers;
using System.Net.Http.Json;
using AAHBRANT.SST.Application.Common.Interfaces;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Integracao.Grh;

// Cliente REST do endpoint de carga inicial do G-RH (contrato acordado em 2026-09-09):
// GET {BaseUrl}/api/integracoes/sst/colaboradores — array JSON puro, sem paginação/envelope.
// Autenticação client-credentials (App Role Grh.LerColaboradores, já atribuída ao service principal
// do SST pelo próprio G-RH) — mesmo padrão de ClientSecretCredential+TokenRequestContext já usado em
// GraphActivityNotificacaoTeamsService. Mapeamento do payload em ColaboradorGrhPayload.cs, reaproveitado
// também pelo consumidor do evento contínuo (ServiceBusColaboradorGrhProcessor) — mesmo contrato de fio.
public class ColaboradorGrhClient : IColaboradorGrhClient
{
    private readonly GrhOptions _opcoes;
    private readonly IHttpClientFactory _httpClientFactory;

    public ColaboradorGrhClient(IOptions<GrhOptions> opcoes, IHttpClientFactory httpClientFactory)
    {
        _opcoes = opcoes.Value;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IReadOnlyList<ColaboradorGrhDto>> ListarTodosAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opcoes.ClientSecret))
            throw new InvalidOperationException(
                "Grh:ClientSecret não configurado — integração com o G-RH ainda não provisionada.");

        var credential = new ClientSecretCredential(_opcoes.TenantId, _opcoes.ClientId, _opcoes.ClientSecret);
        var token = await credential.GetTokenAsync(new TokenRequestContext(new[] { _opcoes.Scope }), ct);

        var httpClient = _httpClientFactory.CreateClient();
        using var requisicao = new HttpRequestMessage(
            HttpMethod.Get, $"{_opcoes.BaseUrl.TrimEnd('/')}/api/integracoes/sst/colaboradores");
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        var resposta = await httpClient.SendAsync(requisicao, ct);
        if (!resposta.IsSuccessStatusCode)
        {
            var corpoResposta = await resposta.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Falha ao consultar colaboradores no G-RH: {(int)resposta.StatusCode} {resposta.StatusCode} — {corpoResposta}");
        }

        var payload = await resposta.Content.ReadFromJsonAsync<List<ColaboradorGrhPayload>>(cancellationToken: ct)
            ?? new List<ColaboradorGrhPayload>();

        return payload.ConvertAll(ColaboradorGrhPayloadMapper.Mapear);
    }
}

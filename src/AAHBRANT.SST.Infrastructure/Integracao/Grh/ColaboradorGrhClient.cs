using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Integracao.Grh;

// Cliente REST do endpoint de carga inicial do G-RH (contrato acordado em 2026-09-09):
// GET {BaseUrl}/api/integracoes/sst/colaboradores — array JSON puro, sem paginação/envelope.
// Autenticação client-credentials (App Role Grh.LerColaboradores, já atribuída ao service principal
// do SST pelo próprio G-RH) — mesmo padrão de ClientSecretCredential+TokenRequestContext já usado em
// GraphActivityNotificacaoTeamsService.
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

        return payload.ConvertAll(Mapear);
    }

    private static ColaboradorGrhDto Mapear(ColaboradorGrhPayload p) => new(
        Cpf: p.Cpf,
        Nome: p.Nome,
        Pis: p.Pis,
        Ctps: p.Ctps,
        DataNascimento: p.Nascimento,
        NomeMae: p.NomeMae,
        Endereco: p.Endereco,
        Municipio: p.Municipio,
        Uf: p.Uf,
        Cep: p.Cep,
        Matricula: p.Matricula,
        DataAdmissao: p.Admissao,
        DataDemissao: p.Desligamento,
        Situacao: MapearSituacao(p.Situacao),
        Salario: p.Salario,
        DataFimExperiencia1: p.Experiencia1Fim,
        DataFimExperiencia2: p.Experiencia2Fim,
        TamanhoBlusaEpi: p.TamanhoBlusa,
        TamanhoCalcaEpi: p.TamanhoCalca,
        TamanhoCalcadoEpi: p.TamanhoCalcado,
        ObraNome: p.ObraNome,
        CargoNome: p.Cargo?.Nome,
        CargoCboCodigo: p.Cargo?.Cbo);

    private static SituacaoTrabalhador MapearSituacao(string? situacao) => situacao?.Trim().ToUpperInvariant() switch
    {
        "ATIVO" => SituacaoTrabalhador.Ativo,
        "AFASTADO" => SituacaoTrabalhador.Afastado,
        "DESLIGADO" => SituacaoTrabalhador.Desligado,
        _ => throw new InvalidOperationException($"Situação '{situacao}' recebida do G-RH não é reconhecida (esperado ATIVO|AFASTADO|DESLIGADO)."),
    };

    private class ColaboradorGrhPayload
    {
        [JsonPropertyName("cpf")] public string Cpf { get; set; } = string.Empty;
        [JsonPropertyName("nome")] public string Nome { get; set; } = string.Empty;
        [JsonPropertyName("pis")] public string? Pis { get; set; }
        [JsonPropertyName("ctps")] public string? Ctps { get; set; }
        [JsonPropertyName("nascimento")] public DateTime? Nascimento { get; set; }
        [JsonPropertyName("nomeMae")] public string? NomeMae { get; set; }
        [JsonPropertyName("endereco")] public string? Endereco { get; set; }
        [JsonPropertyName("municipio")] public string? Municipio { get; set; }
        [JsonPropertyName("uf")] public string? Uf { get; set; }
        [JsonPropertyName("cep")] public string? Cep { get; set; }
        [JsonPropertyName("matricula")] public string Matricula { get; set; } = string.Empty;
        [JsonPropertyName("admissao")] public DateTime Admissao { get; set; }
        [JsonPropertyName("desligamento")] public DateTime? Desligamento { get; set; }
        [JsonPropertyName("situacao")] public string? Situacao { get; set; }
        [JsonPropertyName("salario")] public decimal? Salario { get; set; }
        [JsonPropertyName("experiencia1Fim")] public DateTime? Experiencia1Fim { get; set; }
        [JsonPropertyName("experiencia2Fim")] public DateTime? Experiencia2Fim { get; set; }
        [JsonPropertyName("tamanhoBlusa")] public string? TamanhoBlusa { get; set; }
        [JsonPropertyName("tamanhoCalca")] public string? TamanhoCalca { get; set; }
        [JsonPropertyName("tamanhoCalcado")] public string? TamanhoCalcado { get; set; }
        [JsonPropertyName("obraNome")] public string? ObraNome { get; set; }
        [JsonPropertyName("cargo")] public CargoGrhPayload? Cargo { get; set; }
    }

    private class CargoGrhPayload
    {
        [JsonPropertyName("nome")] public string? Nome { get; set; }
        [JsonPropertyName("cbo")] public string? Cbo { get; set; }
    }
}

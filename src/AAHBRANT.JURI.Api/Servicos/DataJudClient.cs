using System.Text;
using System.Text.Json;
using AAHBRANT.JURI.Api.Modelos;
using Microsoft.Extensions.Options;

namespace AAHBRANT.JURI.Api.Servicos;

// Cliente da API Pública DataJud/CNJ: POST {BaseUrl}/api_publica_{alias}/_search com header
// "Authorization: APIKey ...". Serve para MONITORAR um processo já conhecido pelo número CNJ
// (classe, assuntos, órgão, movimentos). O _source público NÃO traz partes/CNPJ, por isso a
// descoberta por nome fica com o DJEN (DjenClient). A chave é a pública divulgada pelo CNJ e pode
// ser rotacionada; troque em appsettings/variável de ambiente DataJud__ApiKey sem mexer em código.
public class DataJudClient
{
    private readonly HttpClient _http;
    private readonly DataJudOptions _opcoes;

    public DataJudClient(HttpClient http, IOptions<DataJudOptions> opcoes)
    {
        _http = http;
        _opcoes = opcoes.Value;
    }

    public async Task<(List<ProcessoDataJudDto> Processos, string? Erro)> ConsultarPorNumeroAsync(
        string numero, string alias, int maxMovimentos, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opcoes.ApiKey))
            return (new(), "DataJud:ApiKey não configurada.");

        var corpo = JsonSerializer.Serialize(new
        {
            size = 10,
            query = new { match = new { numeroProcesso = NumeroCnj.Limpar(numero) } },
        });

        using var requisicao = new HttpRequestMessage(
            HttpMethod.Post, $"{_opcoes.BaseUrl.TrimEnd('/')}/api_publica_{alias}/_search");
        requisicao.Headers.TryAddWithoutValidation("Authorization", _opcoes.ApiKey.StartsWith("APIKey ", StringComparison.OrdinalIgnoreCase) ? _opcoes.ApiKey : $"APIKey {_opcoes.ApiKey}");
        requisicao.Content = new StringContent(corpo, Encoding.UTF8, "application/json");

        using var resposta = await _http.SendAsync(requisicao, ct);
        var json = await resposta.Content.ReadAsStringAsync(ct);

        if (resposta.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            return (new(), "DataJud respondeu 401: chave APIKey inválida ou rotacionada pelo CNJ. Atualize DataJud:ApiKey.");
        if (resposta.StatusCode == System.Net.HttpStatusCode.NotFound)
            return (new(), $"DataJud respondeu 404: alias 'api_publica_{alias}' não existe.");
        if (!resposta.IsSuccessStatusCode)
            return (new(), $"DataJud respondeu {(int)resposta.StatusCode}: {(json.Length > 300 ? json[..300] : json)}");

        using var doc = JsonDocument.Parse(json);
        var hits = doc.RootElement.GetProperty("hits").GetProperty("hits");
        var processos = new List<ProcessoDataJudDto>();

        foreach (var hit in hits.EnumerateArray())
        {
            var src = hit.GetProperty("_source");
            var movimentos = new List<MovimentoDto>();
            if (src.TryGetProperty("movimentos", out var movs) && movs.ValueKind == JsonValueKind.Array)
            {
                foreach (var m in movs.EnumerateArray())
                {
                    var complementos = new List<string>();
                    if (m.TryGetProperty("complementosTabelados", out var comps) && comps.ValueKind == JsonValueKind.Array)
                        foreach (var cpl in comps.EnumerateArray())
                        {
                            var nome = Texto(cpl, "nome");
                            var descricao = Texto(cpl, "descricao");
                            complementos.Add(string.IsNullOrWhiteSpace(descricao) ? nome ?? string.Empty : $"{descricao}: {nome}");
                        }

                    movimentos.Add(new MovimentoDto(
                        m.TryGetProperty("codigo", out var cod) && cod.ValueKind == JsonValueKind.Number ? cod.GetInt32() : null,
                        Texto(m, "nome"),
                        Texto(m, "dataHora"),
                        complementos));
                }
            }

            var ordenados = movimentos.OrderByDescending(m => m.DataHora).ToList();

            processos.Add(new ProcessoDataJudDto
            {
                Tribunal = Texto(src, "tribunal") ?? alias.ToUpperInvariant(),
                Grau = Texto(src, "grau"),
                Classe = src.TryGetProperty("classe", out var classe) ? Texto(classe, "nome") : null,
                Assuntos = src.TryGetProperty("assuntos", out var assuntos) && assuntos.ValueKind == JsonValueKind.Array
                    ? assuntos.EnumerateArray().Select(a => Texto(a, "nome")).Where(a => a is not null).Select(a => a!).ToList()
                    : new List<string>(),
                OrgaoJulgador = src.TryGetProperty("orgaoJulgador", out var orgao) ? Texto(orgao, "nome") : null,
                DataAjuizamento = Texto(src, "dataAjuizamento"),
                Sistema = src.TryGetProperty("sistema", out var sistema) ? Texto(sistema, "nome") : null,
                Formato = src.TryGetProperty("formato", out var formato) ? Texto(formato, "nome") : null,
                UltimaAtualizacao = Texto(src, "dataHoraUltimaAtualizacao") ?? Texto(src, "@timestamp"),
                TotalMovimentos = movimentos.Count,
                Movimentos = maxMovimentos > 0 ? ordenados.Take(maxMovimentos).ToList() : ordenados,
            });
        }

        // Instância mais recente primeiro (G2 antes de G1 quando o processo subiu).
        processos = processos.OrderByDescending(p => p.UltimaAtualizacao).ToList();
        return (processos, null);
    }

    private static string? Texto(JsonElement el, string prop)
        => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(prop, out var v)
            ? v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Number => v.GetRawText(),
                _ => null,
            }
            : null;
}

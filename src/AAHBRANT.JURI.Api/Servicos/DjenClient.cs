using System.Text.Json;
using AAHBRANT.JURI.Api.Modelos;
using Microsoft.Extensions.Options;

namespace AAHBRANT.JURI.Api.Servicos;

// Cliente da API pública Comunica / Diário de Justiça Eletrônico Nacional (DJEN) do CNJ.
// É a única fonte pública que localiza processos POR NOME DA PARTE: cada intimação/citação publicada
// traz numero_processo, tribunal, órgão, classe, destinatários (com polo) e advogados.
// Sem autenticação. Validado em 2026-09-15 (docs/prototipos/datajud-consulta-cnpjs/comunica_djen_descoberta_resumo.md).
public class DjenClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly DjenOptions _opcoes;
    private readonly ILogger<DjenClient> _logger;

    public DjenClient(HttpClient http, IOptions<DjenOptions> opcoes, ILogger<DjenClient> logger)
    {
        _http = http;
        _opcoes = opcoes.Value;
        _logger = logger;
    }

    public async Task<List<DjenComunicacao>> BuscarAsync(
        string? nomeParte,
        string? siglaTribunal,
        string? numeroProcesso,
        DateOnly? desde,
        DateOnly? ate,
        CancellationToken ct)
    {
        var todas = new List<DjenComunicacao>();

        for (var pagina = 1; pagina <= _opcoes.MaxPaginas; pagina++)
        {
            var query = new List<string>
            {
                $"pagina={pagina}",
                $"itensPorPagina={_opcoes.ItensPorPagina}",
            };
            if (!string.IsNullOrWhiteSpace(nomeParte)) query.Add($"nomeParte={Uri.EscapeDataString(nomeParte)}");
            if (!string.IsNullOrWhiteSpace(siglaTribunal)) query.Add($"siglaTribunal={Uri.EscapeDataString(siglaTribunal.ToUpperInvariant())}");
            if (!string.IsNullOrWhiteSpace(numeroProcesso)) query.Add($"numeroProcesso={NumeroCnj.Limpar(numeroProcesso)}");
            if (desde is not null) query.Add($"dataDisponibilizacaoInicio={desde:yyyy-MM-dd}");
            if (ate is not null) query.Add($"dataDisponibilizacaoFim={ate:yyyy-MM-dd}");

            var url = $"{_opcoes.BaseUrl.TrimEnd('/')}/api/v1/comunicacao?{string.Join("&", query)}";
            using var resposta = await _http.GetAsync(url, ct);
            if (!resposta.IsSuccessStatusCode)
            {
                var corpo = await resposta.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException(
                    $"DJEN respondeu {(int)resposta.StatusCode} {resposta.StatusCode} em {url}: {Truncar(corpo, 300)}");
            }

            var pacote = await resposta.Content.ReadFromJsonAsync<DjenResposta>(Json, ct);
            var itens = pacote?.Items ?? new List<DjenComunicacao>();
            todas.AddRange(itens);

            if (itens.Count < _opcoes.ItensPorPagina) break;
            if (pagina == _opcoes.MaxPaginas)
                _logger.LogWarning("DJEN: limite de {Max} páginas atingido para nomeParte={Nome}; resultado pode estar incompleto.", _opcoes.MaxPaginas, nomeParte);
        }

        return todas;
    }

    // Agrupa comunicações (uma por intimação) em processos (um por número CNJ).
    // filtroJustica: "trabalhista" | "estadual" | "federal" | ... | "todas".
    public static BuscaProcessosResultado Agrupar(
        IEnumerable<DjenComunicacao> comunicacoes,
        string termo,
        string filtroJustica,
        bool incluirComunicacoes,
        bool incluirTexto)
    {
        filtroJustica = string.IsNullOrWhiteSpace(filtroJustica) ? "todas" : filtroJustica.Trim().ToLowerInvariant();

        var porProcesso = new Dictionary<string, List<DjenComunicacao>>();
        var totalComunicacoes = 0;

        foreach (var c in comunicacoes)
        {
            var numero = NumeroCnj.Limpar(c.NumeroProcesso ?? c.NumeroProcessoComMascara ?? string.Empty);
            if (numero.Length != 20) continue;
            if (filtroJustica != "todas" && NumeroCnj.Justica(numero) != filtroJustica) continue;

            totalComunicacoes++;
            if (!porProcesso.TryGetValue(numero, out var lista))
                porProcesso[numero] = lista = new List<DjenComunicacao>();
            lista.Add(c);
        }

        var processos = porProcesso
            .Select(kv => MontarResumo(kv.Key, kv.Value, incluirComunicacoes, incluirTexto))
            .OrderByDescending(p => p.UltimaComunicacao)
            .ToList();

        return new BuscaProcessosResultado
        {
            Termo = termo,
            Justica = filtroJustica,
            TotalComunicacoes = totalComunicacoes,
            TotalProcessos = processos.Count,
            PorTribunal = processos
                .GroupBy(p => p.Tribunal)
                .OrderByDescending(g => g.Count())
                .ToDictionary(g => g.Key, g => g.Count()),
            Processos = processos,
        };
    }

    public static ProcessoResumoDto MontarResumo(
        string numero, List<DjenComunicacao> comunicacoes, bool incluirComunicacoes, bool incluirTexto)
    {
        var ordenadas = comunicacoes.OrderByDescending(c => c.DataDisponibilizacao).ThenByDescending(c => c.Id).ToList();
        var ultima = ordenadas.First();
        var tribunal = NumeroCnj.ResolverTribunal(numero)?.Sigla ?? ultima.SiglaTribunal ?? "?";

        var partes = ordenadas
            .SelectMany(c => c.Destinatarios)
            .Where(d => !string.IsNullOrWhiteSpace(d.Nome))
            .GroupBy(d => d.Nome!.Trim().ToUpperInvariant())
            .Select(g => new ParteDto(g.First().Nome!.Trim(), TraduzirPolo(g.First().Polo)))
            .ToList();

        var advogados = ordenadas
            .SelectMany(c => c.DestinatarioAdvogados)
            .Select(a => a.Advogado)
            .Where(a => a is not null && !string.IsNullOrWhiteSpace(a.Nome))
            .GroupBy(a => $"{a!.NumeroOab}/{a.UfOab}")
            .Select(g => new AdvogadoDto(g.First()!.Nome!.Trim(), $"{g.First()!.NumeroOab}/{g.First()!.UfOab}"))
            .ToList();

        return new ProcessoResumoDto
        {
            Numero = numero,
            NumeroFormatado = NumeroCnj.Formatar(numero),
            Tribunal = tribunal,
            Justica = NumeroCnj.Justica(numero),
            Orgao = ultima.NomeOrgao,
            Classe = ordenadas.Select(c => c.NomeClasse).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)),
            Partes = partes,
            Advogados = advogados,
            TotalComunicacoes = ordenadas.Count,
            PrimeiraComunicacao = ordenadas.Last().DataDisponibilizacao,
            UltimaComunicacao = ultima.DataDisponibilizacao,
            UltimaComunicacaoDetalhe = MapearComunicacao(ultima, incluirTexto),
            Comunicacoes = incluirComunicacoes ? ordenadas.Select(c => MapearComunicacao(c, incluirTexto)).ToList() : null,
        };
    }

    private static ComunicacaoDto MapearComunicacao(DjenComunicacao c, bool incluirTexto) => new(
        c.Id,
        c.DataDisponibilizacao,
        c.TipoComunicacao,
        c.TipoDocumento,
        c.NomeOrgao,
        c.Link,
        incluirTexto ? c.Texto : Truncar(c.Texto, 240));

    private static string TraduzirPolo(string? polo) => polo?.ToUpperInvariant() switch
    {
        "A" => "Ativo",
        "P" => "Passivo",
        null or "" => "Desconhecido",
        _ => polo,
    };

    private static string? Truncar(string? texto, int max)
    {
        if (string.IsNullOrEmpty(texto)) return texto;
        var limpo = System.Text.RegularExpressions.Regex.Replace(texto, "<[^>]+>|\\s+", " ").Trim();
        return limpo.Length <= max ? limpo : limpo[..max] + "…";
    }
}

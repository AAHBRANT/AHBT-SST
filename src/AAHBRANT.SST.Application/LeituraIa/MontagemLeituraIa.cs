using System.Globalization;
using System.Text.RegularExpressions;
using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Ghes.Commands;

namespace AAHBRANT.SST.Application.LeituraIa;

// Parte determinística da leitura com IA: tudo o que dá para conferir sem IA é conferido aqui, com as
// mesmas checagens feitas à mão na leitura do Parque Roger (09/10/2026).
public static partial class MontagemLeituraIa
{
    public record FuncaoExistente(Guid Id, string Nome);
    public record RiscoAtual(int Ghe, string Perigo, int NivelRisco);
    public record ExameAtual(string Funcao, string Exame, int? PeriodicidadeMeses);

    // ---------- PCMSO ----------

    public static int? Meses(string? periodicidade)
    {
        if (string.IsNullOrWhiteSpace(periodicidade)) return null;
        var t = FuncaoSstClassifier.Normalizar(periodicidade);
        if (t.Contains("semestral")) return 6;
        if (t.Contains("trimestral")) return 3;
        if (t.Contains("bienal") || t.Contains("bianual")) return 24;
        if (t.Contains("trienal")) return 36;
        if (t.Contains("anual")) return 12;
        var m = NumeroRegex().Match(t);
        if (!m.Success) return null;
        var n = int.Parse(m.Value, CultureInfo.InvariantCulture);
        return t.Contains("ano") ? n * 12 : n;
    }

    public static (List<ImportarEstruturaExamesFuncao> Exames, List<DivergenciaLeituraIa> Divergencias) MontarExamesPcmso(LeituraPcmsoIa leitura)
    {
        var divergencias = new List<DivergenciaLeituraIa>();
        var porFuncao = new Dictionary<string, (string Nome, List<ImportarEstruturaExame> Exames)>();

        foreach (var q in leitura.Quadros)
        {
            var nome = Espacos(q.Funcao);
            if (nome.Length == 0) continue;
            var periodicos = q.Exames.Where(e => e.Periodico).ToList();
            var periodicidades = q.PeriodicidadesNaOrdem;
            if (periodicos.Count != periodicidades.Count)
                divergencias.Add(new("alta",
                    $"PCMSO, quadro {nome}: {periodicos.Count} exames no periódico e {periodicidades.Count} periodicidades. Confira no PDF."));

            var exames = new List<ImportarEstruturaExame>();
            var i = 0;
            foreach (var e in q.Exames)
            {
                int? meses = null;
                if (e.Periodico)
                {
                    meses = i < periodicidades.Count ? Meses(periodicidades[i]) : null;
                    i++;
                    if (meses is null)
                        divergencias.Add(new("media", $"PCMSO, quadro {nome}: {e.Exame} sem periodicidade legível."));
                }
                var (exame, codigo) = SepararCodigo(e.Exame, e.Codigo);
                exames.Add(new ImportarEstruturaExame(exame, codigo, e.Admissional, e.Periodico, e.RetornoTrabalho, e.MudancaRisco, e.Demissional, meses, null));
            }

            // Quadro repetido da mesma função (página de continuação): fica o mais completo.
            var chave = FuncaoSstClassifier.Normalizar(nome);
            if (!porFuncao.TryGetValue(chave, out var atual) || atual.Exames.Count < exames.Count)
                porFuncao[chave] = (nome, exames);
        }

        var resultado = porFuncao.Values
            .Where(v => v.Exames.Count > 0)
            .Select(v => new ImportarEstruturaExamesFuncao(v.Nome, v.Exames))
            .OrderBy(v => v.Funcao, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        foreach (var vazio in porFuncao.Values.Where(v => v.Exames.Count == 0))
            divergencias.Add(new("media", $"PCMSO: quadro de {vazio.Nome} sem exames."));
        return (resultado, divergencias);
    }

    public static List<DivergenciaLeituraIa> ConferirPcmso(CabecalhoPcmsoIa cab, IEnumerable<string> funcoesPcmso, IEnumerable<string> funcoesGheDaObra, DateTime hoje)
    {
        var d = new List<DivergenciaLeituraIa>();
        if (cab.Validade is { } validade)
        {
            if (validade.Date < hoje) d.Add(new("alta", $"PCMSO vencido em {validade:dd/MM/yyyy}."));
            else if ((validade.Date - hoje).TotalDays <= 30) d.Add(new("media", $"PCMSO vence em {validade:dd/MM/yyyy}."));
        }
        else
        {
            d.Add(new("media", cab.DataElaboracao is { } elab && elab.AddYears(1) < hoje
                ? $"PCMSO sem vigência escrita e elaborado em {elab:dd/MM/yyyy}, há mais de 12 meses."
                : "PCMSO sem vigência escrita."));
        }

        var pcmso = funcoesPcmso.Select(FuncaoSstClassifier.Normalizar).ToHashSet();
        var ghe = funcoesGheDaObra.ToList();
        if (ghe.Count > 0)
        {
            foreach (var f in ghe.Where(f => !pcmso.Contains(FuncaoSstClassifier.Normalizar(f))).Distinct())
                d.Add(new("media", $"{f} está no PGR (GHE) e não tem quadro de exames no PCMSO."));
            var gheNorm = ghe.Select(FuncaoSstClassifier.Normalizar).ToHashSet();
            foreach (var f in funcoesPcmso.Where(f => !gheNorm.Contains(FuncaoSstClassifier.Normalizar(f))).Distinct())
                d.Add(new("media", $"{f} tem quadro no PCMSO e não está em nenhum GHE do PGR."));
        }
        return d;
    }

    // ---------- PGR ----------

    public static List<DivergenciaLeituraIa> ConferirPgr(LeituraPgrIa leitura, IReadOnlyDictionary<(int P, int S), int> matriz, DateTime hoje)
    {
        var d = new List<DivergenciaLeituraIa>();
        if (leitura.Cabecalho.Termino is { } termino)
        {
            if (termino.Date < hoje) d.Add(new("alta", $"PGR vencido em {termino:dd/MM/yyyy}."));
            else if ((termino.Date - hoje).TotalDays <= 30) d.Add(new("alta", $"PGR vence em {termino:dd/MM/yyyy}."));
        }
        else
        {
            d.Add(new("media", "PGR sem data de término da vigência."));
        }

        foreach (var g in leitura.Ghes.OrderBy(g => g.Numero))
        {
            var rotulo = $"GHE {g.Numero:00}";
            if (g.Funcoes.Count == 0) d.Add(new("alta", $"{rotulo} sem funções."));
            if (g.Riscos.Count == 0) d.Add(new("alta", $"{rotulo} sem riscos no inventário."));
            foreach (var r in g.Riscos.Where(r => r.Probabilidade > 0 && r.Severidade > 0))
            {
                var produto = r.Probabilidade * r.Severidade;
                var impresso = NumeroRegex().Match(r.ClassificacaoDocumento ?? "");
                if (impresso.Success && int.Parse(impresso.Value, CultureInfo.InvariantCulture) != produto)
                    d.Add(new("alta", $"{rotulo} · {r.Agente}: P={r.Probabilidade} × G={r.Severidade} = {produto}, o documento imprime {impresso.Value}."));

                if (matriz.TryGetValue((r.Probabilidade, r.Severidade), out var nivel))
                {
                    var faixa = FaixaImpressa(r.ClassificacaoDocumento);
                    if (faixa is not null && !NivelNaFaixa(nivel, faixa))
                        d.Add(new("media", $"{rotulo} · {r.Agente}: classificado como {faixa} no documento; pela matriz de risco o produto {produto} é {NomeNivel(nivel)}."));
                }
                else
                {
                    d.Add(new("alta", $"{rotulo} · {r.Agente}: P={r.Probabilidade}/G={r.Severidade} fora da matriz de risco cadastrada."));
                }
            }
        }
        return d;
    }

    // ---------- Funções ----------

    public static List<FuncaoLidaIa> CasarFuncoes(
        IEnumerable<(string Nome, string? Cbo, int? Ghe, int Exames)> lidas, IReadOnlyList<FuncaoExistente> existentes)
    {
        var porNome = new Dictionary<string, FuncaoExistente>();
        foreach (var f in existentes) porNome.TryAdd(FuncaoSstClassifier.Normalizar(f.Nome), f);
        var porNomeNeutro = new Dictionary<string, FuncaoExistente>();
        foreach (var f in existentes) porNomeNeutro.TryAdd(Neutro(f.Nome), f);

        return lidas
            .GroupBy(l => FuncaoSstClassifier.Normalizar(l.Nome))
            .Select(g =>
            {
                var primeiro = g.First();
                var ghes = g.Where(x => x.Ghe.HasValue).Select(x => x.Ghe!.Value).Distinct().OrderBy(x => x).ToList();
                var exames = g.Sum(x => x.Exames);
                var cbo = g.Select(x => x.Cbo).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
                if (porNome.TryGetValue(g.Key, out var exata))
                    return new FuncaoLidaIa(primeiro.Nome, cbo, ghes, exames, exata.Id, exata.Nome, "existe");
                // Parecida: só gênero diferente (Engenheira/Engenheiro) ou uma das partes de "Ajudante/Servente".
                var parecida = porNomeNeutro.GetValueOrDefault(Neutro(primeiro.Nome))
                    ?? Partes(primeiro.Nome).Select(p => porNome.GetValueOrDefault(p)).FirstOrDefault(p => p is not null);
                return parecida is not null
                    ? new FuncaoLidaIa(primeiro.Nome, cbo, ghes, exames, parecida.Id, parecida.Nome, "parecida")
                    : new FuncaoLidaIa(primeiro.Nome, cbo, ghes, exames, null, null, "nova");
            })
            .OrderBy(f => f.Ghes.Count == 0 ? int.MaxValue : f.Ghes[0]).ThenBy(f => f.NomeDocumento)
            .ToList();
    }

    // ---------- Comparação com a estrutura atual ----------

    public static List<DiferencaLeituraIa> CompararPgr(
        List<ImportarEstruturaGhe> novos,
        IReadOnlyDictionary<int, List<string>> funcoesAtuaisPorGhe,
        IReadOnlyList<RiscoAtual> riscosAtuais,
        IReadOnlyDictionary<(int P, int S), int> matriz)
    {
        var dif = new List<DiferencaLeituraIa>();
        var numerosNovos = novos.Select(g => g.Numero).ToHashSet();
        foreach (var g in novos.Where(g => !funcoesAtuaisPorGhe.ContainsKey(g.Numero)))
            dif.Add(new("incluido", $"GHE {g.Numero:00} · {string.Join(", ", g.Funcoes.Select(f => f.Nome))}", null, "novo"));
        foreach (var (numero, funcoes) in funcoesAtuaisPorGhe.Where(kv => !numerosNovos.Contains(kv.Key)))
            dif.Add(new("removido", $"GHE {numero:00} · {string.Join(", ", funcoes)}", "existia", null));

        foreach (var g in novos.Where(g => funcoesAtuaisPorGhe.ContainsKey(g.Numero)))
        {
            var antes = funcoesAtuaisPorGhe[g.Numero].ToDictionary(FuncaoSstClassifier.Normalizar, f => f);
            var depois = g.Funcoes.ToDictionary(f => FuncaoSstClassifier.Normalizar(f.Nome), f => f.Nome);
            foreach (var (k, nome) in depois.Where(kv => !antes.ContainsKey(kv.Key)))
                dif.Add(new("incluido", $"GHE {g.Numero:00} · função {nome}", null, "incluída"));
            foreach (var (k, nome) in antes.Where(kv => !depois.ContainsKey(kv.Key)))
                dif.Add(new("removido", $"GHE {g.Numero:00} · função {nome}", "existia", null));

            var riscosAntes = riscosAtuais.Where(r => r.Ghe == g.Numero)
                .GroupBy(r => FuncaoSstClassifier.Normalizar(r.Perigo)).ToDictionary(x => x.Key, x => x.First());
            var riscosDepois = g.Riscos.Where(r => r.Probabilidade > 0 && r.Severidade > 0)
                .GroupBy(r => FuncaoSstClassifier.Normalizar(r.Agente)).ToDictionary(x => x.Key, x => x.First());
            foreach (var (k, r) in riscosDepois)
            {
                var nivel = matriz.GetValueOrDefault((r.Probabilidade, r.Severidade));
                if (!riscosAntes.TryGetValue(k, out var a))
                    dif.Add(new("incluido", $"GHE {g.Numero:00} · risco {r.Agente}", null, NomeNivel(nivel)));
                else if (a.NivelRisco != nivel)
                    dif.Add(new("alterado", $"GHE {g.Numero:00} · risco {r.Agente}", NomeNivel(a.NivelRisco), NomeNivel(nivel)));
            }
            foreach (var (k, a) in riscosAntes.Where(kv => !riscosDepois.ContainsKey(kv.Key)))
                dif.Add(new("removido", $"GHE {g.Numero:00} · risco {a.Perigo}", NomeNivel(a.NivelRisco), null));
        }
        return dif;
    }

    public static List<DiferencaLeituraIa> CompararPcmso(List<ImportarEstruturaExamesFuncao> novos, IReadOnlyList<ExameAtual> atuais)
    {
        var dif = new List<DiferencaLeituraIa>();
        var antes = atuais.GroupBy(e => (F: FuncaoSstClassifier.Normalizar(e.Funcao), E: FuncaoSstClassifier.Normalizar(e.Exame)))
            .ToDictionary(g => g.Key, g => g.First());
        var depois = novos.SelectMany(f => f.Exames.Select(e => (Funcao: f.Funcao, Exame: e)))
            .GroupBy(x => (F: FuncaoSstClassifier.Normalizar(x.Funcao), E: FuncaoSstClassifier.Normalizar(x.Exame.Exame)))
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var (k, n) in depois)
        {
            var periodicidade = n.Exame.Periodico ? Periodicidade(n.Exame.PeriodicidadeMeses) : "só ASO";
            if (!antes.TryGetValue(k, out var a))
                dif.Add(new("incluido", $"{n.Funcao} · {n.Exame.Exame}", null, periodicidade));
            else if (a.PeriodicidadeMeses != n.Exame.PeriodicidadeMeses)
                dif.Add(new("alterado", $"{n.Funcao} · {n.Exame.Exame}", Periodicidade(a.PeriodicidadeMeses), periodicidade));
        }
        foreach (var (k, a) in antes.Where(kv => !depois.ContainsKey(kv.Key)))
            dif.Add(new("removido", $"{a.Funcao} · {a.Exame}", Periodicidade(a.PeriodicidadeMeses), null));
        return dif;
    }

    // ---------- auxiliares ----------

    public static string NomeNivel(int nivel) => nivel switch
    {
        1 => "Trivial", 2 => "Baixo", 3 => "Moderado", 4 => "Alto", 5 => "Crítico", _ => "—",
    };

    private static string Periodicidade(int? meses) => meses switch
    {
        null => "sem periodicidade", 6 => "6 meses", 12 => "12 meses", 24 => "24 meses", _ => $"{meses} meses",
    };

    private static string? FaixaImpressa(string? classificacao)
    {
        var t = FuncaoSstClassifier.Normalizar(classificacao ?? "");
        if (t.Contains("critico")) return "Crítico";
        if (t.Contains("alto")) return "Alto";
        if (t.Contains("medio") || t.Contains("moderado")) return "Médio";
        if (t.Contains("baixo") || t.Contains("trivial")) return "Baixo";
        return null;
    }

    // Faixas de 3 níveis dos PGRs (Baixo/Médio/Alto) contra os 5 níveis da matriz do app.
    private static bool NivelNaFaixa(int nivel, string faixa) => faixa switch
    {
        "Baixo" => nivel <= 2,
        "Médio" => nivel == 3,
        "Alto" => nivel >= 4,
        "Crítico" => nivel == 5,
        _ => true,
    };

    private static (string Exame, string? Codigo) SepararCodigo(string exame, string? codigo)
    {
        var m = CodigoRegex().Match(exame);
        var nome = Espacos(CodigoRegex().Replace(exame, " ").Trim(' ', ';', '-'));
        return (nome, string.IsNullOrWhiteSpace(codigo) ? (m.Success ? m.Groups[1].Value : null) : codigo.Trim());
    }

    private static string Espacos(string s) => string.Join(' ', (s ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim();

    private static string Neutro(string nome) =>
        string.Join(' ', FuncaoSstClassifier.Normalizar(nome).Split(' ').Select(p => p.EndsWith('a') && p.Length > 4 ? p[..^1] + "o" : p));

    private static IEnumerable<string> Partes(string nome) =>
        nome.Split(new[] { '/', '(', ')', '-' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => p.Length > 2).Select(FuncaoSstClassifier.Normalizar);

    [GeneratedRegex(@"\d+")]
    private static partial Regex NumeroRegex();

    [GeneratedRegex(@"\[(\d{3,5})\]")]
    private static partial Regex CodigoRegex();
}

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
    public record FuncaoAtual(Guid Id, string Nome);
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
            var doTexto = q.PeriodicidadesNoTexto ?? new List<string>();
            var exames0 = q.Exames;
            var admissionais = exames0.Count(e => e.Admissional);
            // A IA às vezes deixa fora do periódico um exame cuja linha quebra no PDF (ex.: o código do RX na
            // linha de baixo). Se a contagem do texto bate com a do admissional e não com a da IA, o
            // periódico é o admissional (como em todos os quadros do PCMSO do Parque Roger) — com aviso.
            if (doTexto.Count > 0 && doTexto.Count != exames0.Count(e => e.Periodico) && doTexto.Count == admissionais)
            {
                exames0 = exames0.Select(e => e with { Periodico = e.Admissional }).ToList();
                divergencias.Add(new("media",
                    $"PCMSO, quadro {nome}: exames do periódico deduzidos dos do admissional (a leitura não fechou a contagem). Confira no PDF."));
            }
            var periodicos = exames0.Where(e => e.Periodico).ToList();
            var periodicidades = doTexto.Count == periodicos.Count ? doTexto
                : q.PeriodicidadesNaOrdem.Count == periodicos.Count ? q.PeriodicidadesNaOrdem
                : doTexto.Count > 0 ? doTexto : q.PeriodicidadesNaOrdem;
            if (periodicos.Count != periodicidades.Count)
                divergencias.Add(new("alta",
                    $"PCMSO, quadro {nome}: {periodicos.Count} exames no periódico e {periodicidades.Count} periodicidades. Confira no PDF."));

            var exames = new List<ImportarEstruturaExame>();
            var i = 0;
            foreach (var e in exames0)
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

    // funcoesPcmso: nome no documento e função do sistema ligada a ele (casamento); funcoesGheDaObra: função
    // do sistema de cada GHE. Comparação pela função ligada — "Auxiliar de Topógrafo" (PCMSO) e "Auxiliar de
    // Topografia" (GHE) ligados à mesma função não são divergência.
    public static List<DivergenciaLeituraIa> ConferirPcmso(CabecalhoPcmsoIa cab,
        IReadOnlyList<(string Nome, Guid? FuncaoId)> funcoesPcmso, IReadOnlyList<FuncaoAtual> funcoesGheDaObra, DateTime hoje)
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

        if (funcoesGheDaObra.Count > 0)
        {
            var idsPcmso = funcoesPcmso.Where(f => f.FuncaoId.HasValue).Select(f => f.FuncaoId!.Value).ToHashSet();
            var idsGhe = funcoesGheDaObra.Select(f => f.Id).ToHashSet();
            foreach (var f in funcoesGheDaObra.Where(f => !idsPcmso.Contains(f.Id)).DistinctBy(f => f.Id))
                d.Add(new("media", $"{f.Nome} está no PGR (GHE) e não tem quadro de exames no PCMSO."));
            foreach (var f in funcoesPcmso.Where(f => f.FuncaoId is null || !idsGhe.Contains(f.FuncaoId.Value)).DistinctBy(f => f.Nome))
                d.Add(new("media", $"{f.Nome} tem quadro no PCMSO e não está em nenhum GHE do PGR."));
        }
        return d;
    }

    // Valores da coluna PERIODICIDADE do quadro de exames da função, na ordem do texto, sem IA. O texto
    // é dividido em quadros ("QUADRO DE EXAMES"); vale o quadro com "FUNÇÃO: <funcao>" e "EXAMES
    // RECOMENDADOS". Lista vazia quando o quadro não é achado.
    public static List<string> PeriodicidadesNoTexto(string texto, string funcao) =>
        PeriodicidadeRegex().Matches(ExamesDoQuadro(texto, funcao)).Select(m => m.Value.ToUpperInvariant()).ToList();

    // Põe os exames lidos pela IA na ordem em que aparecem no quadro do PDF (pelo código, ou pelo nome) —
    // o casamento das periodicidades é por ordem, e a IA às vezes devolve a lista em outra ordem (leitura
    // real de 10/10/2026: ECG e espirometria trocados em 3 quadros, sem nenhum aviso). Exame não achado
    // no texto vai para o fim, na ordem da IA.
    public static List<ExameQuadroIa> OrdenarExamesPeloTexto(List<ExameQuadroIa> exames, string texto, string funcao)
    {
        var trecho = ExamesDoQuadro(texto, funcao);
        if (trecho.Length == 0) return exames;
        // A ordem é a da PRIMEIRA coluna de cada linha (admissional, mesma ordem do periódico): uma linha do
        // quadro também traz exames de outras colunas (demissional, retorno), então "primeira ocorrência do
        // exame no texto" erra — foi o que trocou ECG e espirometria em 19 quadros num teste real.
        static string Limpo(string t) => string.Join(' ', Regex.Replace(FuncaoSstClassifier.Normalizar(t), "[^a-z0-9]+", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var linhas = trecho.Split('\n');
        var linhaDoCodigo = new Dictionary<string, int>();
        for (var n = 0; n < linhas.Length; n++)
        {
            var primeiro = CodigoRegex().Match(linhas[n]);
            if (primeiro.Success) linhaDoCodigo.TryAdd(primeiro.Groups[1].Value, n);
        }
        var linhasLimpas = linhas.Select(Limpo).ToArray();
        int Posicao(ExameQuadroIa e)
        {
            var (nome, codigo) = SepararCodigo(e.Exame, e.Codigo);
            if (codigo is not null && linhaDoCodigo.TryGetValue(codigo, out var n)) return n;
            var limpo = Limpo(nome);
            for (var i = 0; i < linhasLimpas.Length; i++)
                if (limpo.Length > 0 && linhasLimpas[i].StartsWith(limpo, StringComparison.Ordinal)) return i;
            return int.MaxValue;
        }
        return exames.Select((e, i) => (e, i, p: Posicao(e))).OrderBy(x => x.p).ThenBy(x => x.i).Select(x => x.e).ToList();
    }

    // Parte do quadro de exames da função a partir de "EXAMES RECOMENDADOS" (vazio se não achar).
    private static string ExamesDoQuadro(string texto, string funcao)
    {
        var alvo = FuncaoSstClassifier.Normalizar(funcao);
        foreach (var quadro in QuadroRegex().Split(texto ?? ""))
        {
            var cabecalho = FuncaoNoQuadroRegex().Match(quadro);
            var i = quadro.IndexOf("EXAMES RECOMENDADOS", StringComparison.OrdinalIgnoreCase);
            if (cabecalho.Success && i >= 0 && FuncaoSstClassifier.Normalizar(cabecalho.Groups[1].Value) == alvo)
                return quadro[i..];
        }
        return "";
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
                    ?? Partes(primeiro.Nome).Select(p => porNome.GetValueOrDefault(p)).FirstOrDefault(p => p is not null)
                    ?? existentes.FirstOrDefault(e => MesmoRadical(primeiro.Nome, e.Nome));
                return parecida is not null
                    ? new FuncaoLidaIa(primeiro.Nome, cbo, ghes, exames, parecida.Id, parecida.Nome, "parecida")
                    : new FuncaoLidaIa(primeiro.Nome, cbo, ghes, exames, null, null, "nova");
            })
            .OrderBy(f => f.Ghes.Count == 0 ? int.MaxValue : f.Ghes[0]).ThenBy(f => f.NomeDocumento)
            .ToList();
    }

    // ---------- Comparação com a estrutura atual ----------

    // Funções são comparadas pela função do sistema a que o nome do documento foi ligado (casamento),
    // não pelo texto: "Auxiliar de Topógrafo" e "Auxiliar de Topografia" ligados à mesma função não são
    // mudança. Nome sem função ligada (nova) conta como incluída.
    public static List<DiferencaLeituraIa> CompararPgr(
        List<ImportarEstruturaGhe> novos,
        IReadOnlyDictionary<int, List<FuncaoAtual>> funcoesAtuaisPorGhe,
        IReadOnlyList<RiscoAtual> riscosAtuais,
        IReadOnlyDictionary<(int P, int S), int> matriz,
        IReadOnlyDictionary<string, Guid?> funcaoLigada)
    {
        var dif = new List<DiferencaLeituraIa>();
        var numerosNovos = novos.Select(g => g.Numero).ToHashSet();
        foreach (var g in novos.Where(g => !funcoesAtuaisPorGhe.ContainsKey(g.Numero)))
            dif.Add(new("incluido", $"GHE {g.Numero:00} · {string.Join(", ", g.Funcoes.Select(f => f.Nome))}", null, "novo"));
        foreach (var (numero, funcoes) in funcoesAtuaisPorGhe.Where(kv => !numerosNovos.Contains(kv.Key)))
            dif.Add(new("removido", $"GHE {numero:00} · {string.Join(", ", funcoes.Select(f => f.Nome))}", "existia", null));

        foreach (var g in novos.Where(g => funcoesAtuaisPorGhe.ContainsKey(g.Numero)))
        {
            var idsAntes = funcoesAtuaisPorGhe[g.Numero].Select(f => f.Id).ToHashSet();
            var idsDepois = new HashSet<Guid>();
            foreach (var f in g.Funcoes)
            {
                var id = funcaoLigada.GetValueOrDefault(FuncaoSstClassifier.Normalizar(f.Nome));
                if (id is { } existente) idsDepois.Add(existente);
                if (id is null || !idsAntes.Contains(id.Value))
                    dif.Add(new("incluido", $"GHE {g.Numero:00} · função {f.Nome}", null, "incluída"));
            }
            foreach (var f in funcoesAtuaisPorGhe[g.Numero].Where(f => !idsDepois.Contains(f.Id)))
                dif.Add(new("removido", $"GHE {g.Numero:00} · função {f.Nome}", "existia", null));

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

    // Mesmas palavras com o mesmo começo (até 6 letras): "Auxiliar de Topógrafo" ~ "Auxiliar de Topografia".
    private static bool MesmoRadical(string a, string b)
    {
        var pa = FuncaoSstClassifier.Normalizar(a).Split(' ');
        var pb = FuncaoSstClassifier.Normalizar(b).Split(' ');
        if (pa.Length != pb.Length) return false;
        for (var i = 0; i < pa.Length; i++)
        {
            var n = Math.Min(6, Math.Min(pa[i].Length, pb[i].Length));
            if (n < 2 || string.CompareOrdinal(pa[i], 0, pb[i], 0, n) != 0) return false;
        }
        return true;
    }

    private static IEnumerable<string> Partes(string nome) =>
        nome.Split(new[] { '/', '(', ')', '-' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => p.Length > 2).Select(FuncaoSstClassifier.Normalizar);

    [GeneratedRegex(@"\d+")]
    private static partial Regex NumeroRegex();

    [GeneratedRegex(@"\[(\d{3,5})\]")]
    private static partial Regex CodigoRegex();

    [GeneratedRegex(@"QUADRO\s+DE\s+EXAMES", RegexOptions.IgnoreCase)]
    private static partial Regex QuadroRegex();

    [GeneratedRegex(@"FUN[ÇC][ÃA]O\s*:\s*(.+?)\s+(?:CBO\s*:|GHE\s*:|$)", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex FuncaoNoQuadroRegex();

    [GeneratedRegex(@"\b(ANUAL|BIENAL|BIANUAL|SEMESTRAL|TRIMESTRAL|TRIENAL)\b", RegexOptions.IgnoreCase)]
    private static partial Regex PeriodicidadeRegex();
}

using System.Globalization;
using System.Text;
using AAHBRANT.SST.Application.Ghes.Commands;
using AAHBRANT.SST.Application.LeituraIa;
using AAHBRANT.SST.Infrastructure.Integracao.SuporteIa;

namespace AAHBRANT.SST.Infrastructure.LeituraIa;

// Leitura de PGR e PCMSO pelo gpt5mini (Azure OpenAI), em partes, do jeito que funcionou na leitura
// manual do Parque Roger (09/10/2026): primeiro um índice do documento (GHE ou quadros de exames, com as
// páginas de cada um), depois uma chamada por GHE / por quadro só com as páginas dele. Cada resposta é
// pequena, o que cabe no limite de saída do modelo e deixa as chamadas em paralelo.
public class AzureOpenAiLeitorDocumentoSst : ILeitorDocumentoSstIa
{
    private const int Paralelismo = 4;
    private const int MaxPaginasPorParte = 10;

    private readonly AzureOpenAiChatJsonCliente _chat;

    public AzureOpenAiLeitorDocumentoSst(AzureOpenAiChatJsonCliente chat) => _chat = chat;

    // ===================== PGR =====================

    private const string InstrucoesPgr = """
        Você lê PGR (Programa de Gerenciamento de Riscos, NR-01) de obras de construção civil no Brasil.
        O texto foi extraído de um PDF e vem marcado com "=== PÁGINA n ===". Tabelas podem estar quebradas
        em várias linhas. Responda SOMENTE com o que está escrito no documento: não invente, não corrija,
        não complete. Campo ausente ou ilegível = null. Datas no formato AAAA-MM-DD.
        """;

    private record GheIndice(int Numero, string? Setor, string? Jornada, string? Ambiente, string? AtividadesCriticas,
        List<FuncaoIndice> Funcoes, int? PaginaInicialInventario, int? PaginaFinalInventario);
    private record FuncaoIndice(string Nome, string? Cbo, int? QuantidadeExpostos);
    private record IndicePgr(string? Responsavel, string? Registro, string? Revisao, string? Inicio, string? RevisaoSugerida,
        string? Termino, List<GheIndice> Ghes, List<int> PaginasPlanoAcao);
    private record RiscoLido(string Tipo, string Agente, string? Danos, string? Avaliacao, string? Epc, string? Epi,
        int Probabilidade, int Severidade, string? Classificacao, string? Monitoramento);
    private record RiscosGhe(List<RiscoLido> Riscos);
    private record PlanoLido(List<string> Acoes);

    public async Task<LeituraPgrIa> LerPgrAsync(IReadOnlyList<string> paginas, Func<string, int, int, Task> progresso, CancellationToken ct)
    {
        await progresso("Lendo o índice do PGR (GHE e funções)", 0, 1);
        var indice = await _chat.ObterAsync<IndicePgr>(InstrucoesPgr, $"""
            Leia o PGR inteiro e devolva:
            - responsavel (nome do responsável técnico pela elaboração), registro (CREA/CRM/registro profissional), revisao (número da revisão do documento);
            - inicio, revisaoSugerida e termino da vigência do programa;
            - ghes: TODOS os Grupos Homogêneos de Exposição (GHE). Para cada um: numero (inteiro), setor, jornada,
              ambiente (descrição do ambiente de trabalho), atividadesCriticas, funcoes (nome como escrito, cbo só dígitos,
              quantidadeExpostos) e as páginas onde está o INVENTÁRIO DE RISCOS daquele GHE (paginaInicialInventario,
              paginaFinalInventario — a tabela de riscos com probabilidade e severidade, não a descrição do GHE);
            - paginasPlanoAcao: páginas do plano de ação / cronograma de ações.
            Se a tabela-resumo de funções por GHE e a descrição detalhada divergirem, use a tabela do inventário.

            {Juntar(paginas, 1, paginas.Count)}
            """, "indice_pgr", SchemaIndicePgr, ct, maxTokens: 16000, timeoutSegundos: 300);

        var ghes = indice.Ghes.Where(g => g.Numero > 0).GroupBy(g => g.Numero).Select(g => g.First()).OrderBy(g => g.Numero).ToList();
        var total = ghes.Count + 2;
        var feitos = 1;
        var trava = new SemaphoreSlim(1, 1);
        async Task Avancar(string etapa)
        {
            await trava.WaitAsync(ct);
            try { await progresso(etapa, ++feitos, total); }
            finally { trava.Release(); }
        }
        await progresso($"Inventário de riscos: 0 de {ghes.Count} GHE", feitos, total);

        var limite = new SemaphoreSlim(Paralelismo);
        var concluidos = 0;
        var tarefas = ghes.Select(async g =>
        {
            await limite.WaitAsync(ct);
            try
            {
                var trecho = Trecho(paginas, g.PaginaInicialInventario, g.PaginaFinalInventario);
                var lidos = await _chat.ObterAsync<RiscosGhe>(InstrucoesPgr, $"""
                    Extraia TODAS as linhas do inventário de riscos do GHE {g.Numero:00} (funções: {string.Join(", ", g.Funcoes.Select(f => f.Nome))}).
                    Ignore os outros GHE que aparecerem no trecho. Para cada risco: tipo (Físico, Químico, Biológico,
                    Ergonômico, Acidentes etc., como no documento), agente (o perigo/agente), danos (possíveis danos à saúde),
                    avaliacao (Qualitativa/Quantitativa), epc (controles existentes/EPC), epi, probabilidade e severidade
                    (inteiros como impressos; linha "não há exposição" = 0 e 0), classificacao (texto exato impresso,
                    ex.: "6 - Médio Risco") e monitoramento.

                    {trecho}
                    """, "riscos_ghe", SchemaRiscos, ct, maxTokens: 12000, timeoutSegundos: 240);

                var ghe = new ImportarEstruturaGhe(
                    g.Numero, g.Setor, g.Jornada, g.Ambiente, g.AtividadesCriticas, null, null,
                    g.Funcoes.Where(f => !string.IsNullOrWhiteSpace(f.Nome))
                        .Select(f => new ImportarEstruturaFuncao(f.Nome.Trim(), SoDigitos(f.Cbo), null, f.QuantidadeExpostos, null)).ToList(),
                    lidos.Riscos.Where(r => !string.IsNullOrWhiteSpace(r.Agente))
                        .Select(r => new ImportarEstruturaRisco(r.Tipo, r.Agente.Trim(), r.Danos, r.Avaliacao, r.Epc, r.Epi,
                            Math.Max(0, r.Probabilidade), Math.Max(0, r.Severidade), r.Classificacao, r.Monitoramento)).ToList());
                await Avancar($"Inventário de riscos: {Interlocked.Increment(ref concluidos)} de {ghes.Count} GHE");
                return ghe;
            }
            finally
            {
                limite.Release();
            }
        }).ToList();
        var lidosGhe = (await Task.WhenAll(tarefas)).OrderBy(g => g.Numero).ToList();

        var plano = new List<string>();
        if (indice.PaginasPlanoAcao.Count > 0)
        {
            var ini = indice.PaginasPlanoAcao.Min();
            var fim = indice.PaginasPlanoAcao.Max();
            var lido = await _chat.ObterAsync<PlanoLido>(InstrucoesPgr, $"""
                Liste as ações do plano de ação / cronograma de ações deste PGR, uma por item, com o texto do documento
                (o quê; acrescente quem/onde/quando entre parênteses se estiverem escritos). Sem repetir.

                {Trecho(paginas, ini, fim)}
                """, "plano_pgr", SchemaPlano, ct, maxTokens: 8000, timeoutSegundos: 180);
            plano = lido.Acoes.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).Distinct().ToList();
        }
        await progresso("Conferindo a leitura", total, total);

        return new LeituraPgrIa(
            new CabecalhoPgrIa(indice.Responsavel, indice.Registro, indice.Revisao, Data(indice.Inicio), Data(indice.RevisaoSugerida), Data(indice.Termino)),
            lidosGhe, plano);
    }

    // ===================== PCMSO =====================

    private const string InstrucoesPcmso = """
        Você lê PCMSO (Programa de Controle Médico de Saúde Ocupacional, NR-07) de obras no Brasil. O texto foi
        extraído de um PDF e vem marcado com "=== PÁGINA n ===". Tabelas podem estar quebradas em várias linhas e
        as colunas podem aparecer desalinhadas. Responda SOMENTE com o que está escrito: não invente, não corrija,
        não complete. Campo ausente ou ilegível = null. Datas no formato AAAA-MM-DD.
        """;

    private record QuadroIndice(string Funcao, string? Ghe, string? Cbo, int? PaginaInicial, int? PaginaFinal);
    private record IndicePcmso(string? MedicoNome, string? MedicoCrm, string? DataElaboracao, string? Validade, List<QuadroIndice> Quadros);
    private record ExameLido(string Exame, string? Codigo, bool Admissional, bool Periodico, bool RetornoTrabalho, bool MudancaRisco, bool Demissional);
    private record QuadroLido(List<ExameLido> Exames, List<string> PeriodicidadesNaOrdem);

    public async Task<LeituraPcmsoIa> LerPcmsoAsync(IReadOnlyList<string> paginas, Func<string, int, int, Task> progresso, CancellationToken ct)
    {
        await progresso("Lendo o índice do PCMSO (quadros de exames)", 0, 1);
        var indice = await _chat.ObterAsync<IndicePcmso>(InstrucoesPcmso, $"""
            Leia o PCMSO inteiro e devolva:
            - medicoNome e medicoCrm do médico responsável/coordenador; dataElaboracao; validade (fim da vigência, se escrita);
            - quadros: TODOS os quadros de exames por função ("QUADRO DE EXAMES"), um por função, com funcao (como escrita),
              ghe, cbo (só dígitos) e as páginas do quadro (paginaInicial, paginaFinal).

            {Juntar(paginas, 1, paginas.Count)}
            """, "indice_pcmso", SchemaIndicePcmso, ct, maxTokens: 12000, timeoutSegundos: 300);

        var quadros = indice.Quadros.Where(q => !string.IsNullOrWhiteSpace(q.Funcao)).ToList();
        var total = quadros.Count + 1;
        var feitos = 1;
        var trava = new SemaphoreSlim(1, 1);
        await progresso($"Quadros de exames: 0 de {quadros.Count}", feitos, total);

        var limite = new SemaphoreSlim(Paralelismo);
        var concluidos = 0;
        var tarefas = quadros.Select(async q =>
        {
            await limite.WaitAsync(ct);
            try
            {
                var lido = await _chat.ObterAsync<QuadroLido>(InstrucoesPcmso, $"""
                    Leia o quadro de exames da função "{q.Funcao}" (ignore quadros de outras funções no trecho).
                    - exames: cada exame recomendado UMA vez, na ordem da coluna do periódico (exames que não estão no
                      periódico vêm depois), com o nome sem o código, codigo (número entre colchetes, ex.: 0281) e em quais
                      colunas ele aparece: admissional, periodico, retornoTrabalho, mudancaRisco (mudança de riscos/função),
                      demissional.
                    - periodicidadesNaOrdem: os valores da coluna PERIODICIDADE (ANUAL, BIENAL, SEMESTRAL...) exatamente na
                      ordem em que aparecem, um por item, mesmo que pareçam desalinhados das linhas dos exames. Não tente
                      associar cada periodicidade a um exame.

                    {Trecho(paginas, q.PaginaInicial, q.PaginaFinal)}
                    """, "quadro_pcmso", SchemaQuadro, ct, maxTokens: 8000, timeoutSegundos: 180);

                var quadro = new QuadroPcmsoIa(q.Funcao.Trim(), q.Ghe, SoDigitos(q.Cbo),
                    lido.Exames.Where(e => !string.IsNullOrWhiteSpace(e.Exame))
                        .Select(e => new ExameQuadroIa(e.Exame.Trim(), SoDigitos(e.Codigo), e.Admissional, e.Periodico, e.RetornoTrabalho, e.MudancaRisco, e.Demissional))
                        .ToList(),
                    lido.PeriodicidadesNaOrdem.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToList());

                await trava.WaitAsync(ct);
                try { await progresso($"Quadros de exames: {++concluidos} de {quadros.Count}", ++feitos, total); }
                finally { trava.Release(); }
                return quadro;
            }
            finally
            {
                limite.Release();
            }
        }).ToList();
        var lidos = await Task.WhenAll(tarefas);

        return new LeituraPcmsoIa(
            new CabecalhoPcmsoIa(indice.MedicoNome, indice.MedicoCrm, Data(indice.DataElaboracao), Data(indice.Validade)),
            lidos.ToList());
    }

    // ===================== auxiliares =====================

    private static string Juntar(IReadOnlyList<string> paginas, int de, int ate)
    {
        var sb = new StringBuilder();
        for (var n = Math.Max(1, de); n <= Math.Min(paginas.Count, ate); n++)
            sb.Append("=== PÁGINA ").Append(n).AppendLine(" ===").AppendLine(paginas[n - 1]);
        return sb.ToString();
    }

    // Páginas indicadas no índice com uma de folga (tabela que começa no fim da página anterior). Sem
    // páginas válidas, vai o documento inteiro — a resposta continua pequena, só a entrada é maior.
    private static string Trecho(IReadOnlyList<string> paginas, int? inicial, int? final)
    {
        if (inicial is not { } ini || ini < 1 || ini > paginas.Count)
            return Juntar(paginas, 1, paginas.Count);
        var fim = final is { } f && f >= ini ? f : ini;
        fim = Math.Min(fim, ini + MaxPaginasPorParte - 1);
        return Juntar(paginas, ini - 1, fim + 1);
    }

    private static DateTime? Data(string? texto) =>
        DateTime.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d)
            ? DateTime.SpecifyKind(d.Date, DateTimeKind.Utc) : null;

    private static string? SoDigitos(string? s)
    {
        var d = new string((s ?? "").Where(char.IsDigit).ToArray());
        return d.Length == 0 ? null : d;
    }

    // ---- JSON schemas (strict: todo campo obrigatório; opcional = tipo com null) ----

    private static object Texto(bool nulo = true) => new { type = nulo ? new[] { "string", "null" } : new[] { "string" } };
    private static object Inteiro(bool nulo = true) => new { type = nulo ? new[] { "integer", "null" } : new[] { "integer" } };
    private static object Booleano() => new { type = "boolean" };
    private static object Lista(object item) => new { type = "array", items = item };
    private static object Objeto(Dictionary<string, object> campos) => new
    {
        type = "object",
        properties = campos,
        required = campos.Keys.ToArray(),
        additionalProperties = false,
    };

    private static readonly object SchemaIndicePgr = Objeto(new()
    {
        ["responsavel"] = Texto(), ["registro"] = Texto(), ["revisao"] = Texto(),
        ["inicio"] = Texto(), ["revisaoSugerida"] = Texto(), ["termino"] = Texto(),
        ["ghes"] = Lista(Objeto(new()
        {
            ["numero"] = Inteiro(false), ["setor"] = Texto(), ["jornada"] = Texto(), ["ambiente"] = Texto(),
            ["atividadesCriticas"] = Texto(),
            ["funcoes"] = Lista(Objeto(new() { ["nome"] = Texto(false), ["cbo"] = Texto(), ["quantidadeExpostos"] = Inteiro() })),
            ["paginaInicialInventario"] = Inteiro(), ["paginaFinalInventario"] = Inteiro(),
        })),
        ["paginasPlanoAcao"] = Lista(new { type = "integer" }),
    });

    private static readonly object SchemaRiscos = Objeto(new()
    {
        ["riscos"] = Lista(Objeto(new()
        {
            ["tipo"] = Texto(false), ["agente"] = Texto(false), ["danos"] = Texto(), ["avaliacao"] = Texto(),
            ["epc"] = Texto(), ["epi"] = Texto(), ["probabilidade"] = Inteiro(false), ["severidade"] = Inteiro(false),
            ["classificacao"] = Texto(), ["monitoramento"] = Texto(),
        })),
    });

    private static readonly object SchemaPlano = Objeto(new() { ["acoes"] = Lista(new { type = "string" }) });

    private static readonly object SchemaIndicePcmso = Objeto(new()
    {
        ["medicoNome"] = Texto(), ["medicoCrm"] = Texto(), ["dataElaboracao"] = Texto(), ["validade"] = Texto(),
        ["quadros"] = Lista(Objeto(new()
        {
            ["funcao"] = Texto(false), ["ghe"] = Texto(), ["cbo"] = Texto(), ["paginaInicial"] = Inteiro(), ["paginaFinal"] = Inteiro(),
        })),
    });

    private static readonly object SchemaQuadro = Objeto(new()
    {
        ["exames"] = Lista(Objeto(new()
        {
            ["exame"] = Texto(false), ["codigo"] = Texto(), ["admissional"] = Booleano(), ["periodico"] = Booleano(),
            ["retornoTrabalho"] = Booleano(), ["mudancaRisco"] = Booleano(), ["demissional"] = Booleano(),
        })),
        ["periodicidadesNaOrdem"] = Lista(new { type = "string" }),
    });
}

using System.Text;
using AAHBRANT.SST.Application.GeracaoPgr;

namespace AAHBRANT.SST.Infrastructure.Integracao.SuporteIa;

/// <summary>
/// IA do gerador de PGR pela EAP (gpt5mini, saída JSON estrita). Três chamadas: ler a EAP, agrupar
/// atividades em GHE com as funções, e gerar riscos e medidas por atividade. Tudo é sugestão; a
/// conferência (P/S dentro da matriz, normas citadas só entre as validadas, atividades que existem na
/// EAP) é feita no servidor, e a aprovação é do Engenheiro de Segurança.
/// </summary>
public class AzureOpenAiGeradorPgr : IGeradorPgrIa
{
    private readonly AzureOpenAiChatJsonCliente _chat;

    public AzureOpenAiGeradorPgr(AzureOpenAiChatJsonCliente chat) => _chat = chat;

    // ---- 1. Ler a EAP ----

    private const string InstrucoesEap = """
        Você lê a EAP (Estrutura Analítica do Projeto) de uma obra da AAHBRANT Engenharia, extraída de
        planilha, MS Project ou PDF. Devolva os itens na ordem em que aparecem. Responda só com o JSON.
        - codigo: o número do item na EAP (ex.: "1", "1.2", "2.3.1"). Se não houver, numere na ordem.
        - nome: o nome do item como está, sem inventar nem resumir.
        - nivel: 1 para pacote/etapa, 2 ou mais para atividades dentro dele.
        - periodo: datas ou mês de início e fim, se houver; senão null.
        - usar: false para itens sem frente de trabalho no canteiro (orçamento, compras, medições,
          projetos, documentação, marcos sem execução); true para os demais.
        - conferir: true quando o nome estiver ilegível, cortado ou duvidoso.
        Ignore cabeçalhos, totais, custos e colunas que não sejam a estrutura.
        """;

    private static readonly object SchemaEap = new
    {
        type = "object",
        additionalProperties = false,
        required = new[] { "itens" },
        properties = new
        {
            itens = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[] { "codigo", "nome", "nivel", "periodo", "usar", "conferir" },
                    properties = new
                    {
                        codigo = new { type = "string" },
                        nome = new { type = "string" },
                        nivel = new { type = "integer" },
                        periodo = new { type = new[] { "string", "null" } },
                        usar = new { type = "boolean" },
                        conferir = new { type = "boolean" },
                    },
                },
            },
        },
    };

    public async Task<List<ItemEapGerado>> LerEapAsync(string textoEap, CancellationToken ct)
    {
        var r = await _chat.ObterAsync<RespostaEap>(InstrucoesEap, textoEap, "eap_obra", SchemaEap, ct, maxTokens: 16000, timeoutSegundos: 180);
        return (r.Itens ?? new())
            .Where(i => !string.IsNullOrWhiteSpace(i.Nome))
            .Select(i => new ItemEapGerado
            {
                Codigo = (i.Codigo ?? "").Trim(),
                Nome = i.Nome!.Trim(),
                Nivel = Math.Max(1, i.Nivel),
                Periodo = i.Periodo,
                Usar = i.Usar,
                Conferir = i.Conferir,
            })
            .ToList();
    }

    // ---- 2. GHE e funções ----

    private const string InstrucoesGhe = """
        Você é engenheiro de Segurança do Trabalho da AAHBRANT Engenharia montando o PGR de uma obra
        (NR-01) a partir das atividades da EAP. Agrupe as atividades em GHE (Grupos Homogêneos de
        Exposição): atividades com exposição parecida e as mesmas funções ficam no mesmo GHE. Responda só
        com o JSON.
        - nome: nome curto do GHE (ex.: "Movimentação de terra", "Estrutura de concreto").
        - ambiente: onde o trabalho acontece (céu aberto, altura, escavação etc.), em uma frase.
        - jornada: null (será preenchida pela obra).
        - atividades: os códigos EXATOS das atividades da lista que pertencem ao GHE. Cada atividade em
          um único GHE.
        - funcoes: quem executa. Use EXATAMENTE os nomes da lista de funções cadastradas quando houver
          equivalente; só proponha função nova quando nenhuma cadastrada servir.
        - descricao (por função): o que a função faz nestas atividades, em linguagem clara para o
          trabalhador, até 600 caracteres. Não amplie além das atividades do GHE.
        """;

    private static readonly object SchemaGhe = new
    {
        type = "object",
        additionalProperties = false,
        required = new[] { "ghes" },
        properties = new
        {
            ghes = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[] { "nome", "ambiente", "jornada", "atividades", "funcoes" },
                    properties = new
                    {
                        nome = new { type = "string" },
                        ambiente = new { type = new[] { "string", "null" } },
                        jornada = new { type = new[] { "string", "null" } },
                        atividades = new { type = "array", items = new { type = "string" } },
                        funcoes = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "object",
                                additionalProperties = false,
                                required = new[] { "nome", "descricao" },
                                properties = new { nome = new { type = "string" }, descricao = new { type = "string" } },
                            },
                        },
                    },
                },
            },
        },
    };

    public async Task<List<GheGerado>> SugerirGhesAsync(IReadOnlyList<ItemEapGerado> atividades, IReadOnlyList<string> funcoesCadastradas, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Atividades da EAP (código | nome):");
        foreach (var a in atividades) sb.AppendLine($"{a.Codigo} | {a.Nome}");
        sb.AppendLine();
        sb.AppendLine("Funções cadastradas:");
        foreach (var f in funcoesCadastradas) sb.AppendLine($"- {f}");

        var r = await _chat.ObterAsync<RespostaGhe>(InstrucoesGhe, sb.ToString(), "ghes_pgr", SchemaGhe, ct, maxTokens: 10000, timeoutSegundos: 150);
        return (r.Ghes ?? new()).Select(g => new GheGerado
        {
            Nome = (g.Nome ?? "").Trim(),
            Ambiente = g.Ambiente,
            Jornada = g.Jornada,
            Atividades = (g.Atividades ?? new()).Select(c => new AtividadeGerada { CodigoEap = c.Trim() }).ToList(),
            Funcoes = (g.Funcoes ?? new()).Where(f => !string.IsNullOrWhiteSpace(f.Nome))
                .Select(f => new FuncaoGerada { Nome = f.Nome!.Trim(), Descricao = f.Descricao?.Trim() }).ToList(),
        }).ToList();
    }

    // ---- 3. Riscos e medidas por atividade ----

    private const string InstrucoesRiscos = """
        Você é engenheiro de Segurança do Trabalho da AAHBRANT Engenharia montando o inventário de riscos
        do PGR (NR-01) para UMA atividade de obra. Responda só com o JSON.
        - Liste os perigos realmente presentes na atividade (normalmente 3 a 8), de todos os tipos
          aplicáveis: Físico, Químico, Biológico, Ergonômico, Acidente.
        - perigo: o agente ou a fonte, curto (ex.: "Queda de altura na borda da laje", "Ruído do vibrador").
        - danos: possíveis lesões ou doenças.
        - probabilidade e severidade: inteiros de 1 até o máximo informado da matriz, avaliados para a
          condição usual da atividade em obra, antes de controles adicionais.
        - base: normas aplicáveis, citando SOMENTE itens da lista de requisitos legais validados, no
          formato "NR-35" ou "NR-18" (o código da norma). Lista vazia se nenhuma se aplicar.
        - medidas: medidas de prevenção no imperativo, até 200 caracteres cada, em linguagem que o
          trabalhador entenda, seguindo a hierarquia da NR-01 (eliminação, proteção coletiva,
          administrativa, EPI por último). fase: "Antes" (preparação, inspeção, liberação, PT/APR),
          "Durante" (execução) ou "Apos" (desmobilização, limpeza, guarda). 2 a 6 medidas por perigo.
          categoria: "Eliminacao" (elimina o perigo), "EPC" (proteção coletiva), "Administrativa"
          (procedimento, treinamento, sinalização, inspeção) ou "EPI" (equipamento de proteção individual).
          Cada medida tem uma única categoria; não misture EPC e EPI no mesmo texto.
        Não invente procedimento interno nem nome de documento da empresa.
        """;

    private static readonly object SchemaRiscos = new
    {
        type = "object",
        additionalProperties = false,
        required = new[] { "riscos" },
        properties = new
        {
            riscos = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[] { "tipo", "perigo", "danos", "probabilidade", "severidade", "base", "medidas" },
                    properties = new
                    {
                        tipo = new { type = "string", @enum = new[] { "Físico", "Químico", "Biológico", "Ergonômico", "Acidente" } },
                        perigo = new { type = "string" },
                        danos = new { type = "string" },
                        probabilidade = new { type = "integer" },
                        severidade = new { type = "integer" },
                        @base = new { type = "array", items = new { type = "string" } },
                        medidas = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "object",
                                additionalProperties = false,
                                required = new[] { "fase", "texto", "categoria" },
                                properties = new
                                {
                                    fase = new { type = "string", @enum = new[] { "Antes", "Durante", "Apos" } },
                                    texto = new { type = "string" },
                                    categoria = new { type = "string", @enum = new[] { "Eliminacao", "EPC", "Administrativa", "EPI" } },
                                },
                            },
                        },
                    },
                },
            },
        },
    };

    public async Task<List<RiscoGerado>> GerarRiscosAsync(ContextoRiscosAtividade c, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Atividade: {c.Atividade}");
        sb.AppendLine($"GHE: {c.Ghe}");
        sb.AppendLine($"Ambiente: {c.Ambiente ?? "não informado"}");
        sb.AppendLine($"Funções: {string.Join(", ", c.Funcoes)}");
        sb.AppendLine($"Matriz de risco: probabilidade de 1 a {c.MaxProbabilidade}; severidade de 1 a {c.MaxSeveridade}.");
        sb.AppendLine("Requisitos legais validados:");
        foreach (var q in c.RequisitosLegais) sb.AppendLine($"- {q}");
        if (c.RequisitosLegais.Count == 0) sb.AppendLine("(nenhum — deixe base vazia)");

        var r = await _chat.ObterAsync<RespostaRiscos>(InstrucoesRiscos, sb.ToString(), "riscos_atividade", SchemaRiscos, ct, maxTokens: 8000, timeoutSegundos: 120);
        return (r.Riscos ?? new()).Where(x => !string.IsNullOrWhiteSpace(x.Perigo)).Select(x => new RiscoGerado
        {
            Tipo = x.Tipo ?? "",
            Perigo = x.Perigo!.Trim(),
            Danos = x.Danos,
            Probabilidade = x.Probabilidade,
            Severidade = x.Severidade,
            Base = x.Base ?? new(),
            Medidas = (x.Medidas ?? new()).Select(m => new MedidaGerada
            {
                Fase = m.Fase == "Antes" ? 1 : m.Fase == "Apos" ? 3 : 2,
                Texto = (m.Texto ?? "").Trim(),
                Categoria = CategoriasMedida.Normalizar(m.Categoria),
            }).ToList(),
        }).ToList();
    }

    // ---- 4. Emergência: escolher o local mais adequado e justificar ----

    private const string InstrucoesEmergencia = """
        Você é engenheiro de Segurança do Trabalho da AAHBRANT Engenharia montando o plano de emergência do PGR
        de uma obra. O mapa já devolveu locais REAIS, com distância e tempo de rota de carro a partir da obra.
        Responda só com o JSON.
        - recomendados: escolha UM candidato para cada tipo presente (Hospital, Corpo de Bombeiros, Delegacia
          ou posto de polícia), usando o campo id exatamente como veio. Critérios, nesta ordem: para hospital,
          prefira o que atende urgência (atendeUrgencia = true) quando o tempo de rota for razoável (até cerca
          de 10 minutos a mais que o mais rápido) e considere os riscos da obra (queda de altura, soterramento,
          eletricidade, esmagamento pedem pronto-socorro com trauma); depois o menor tempo de rota.
        - justificativa: uma frase curta (até 160 caracteres) dizendo por que o escolhido é a melhor opção,
          citando tempo ou distância e, se for o caso, o atendimento de urgência. Use SÓ os dados fornecidos.
          Nunca invente nome, endereço, telefone, rua nem capacidade do local.
        - cenarios: dos cenários possíveis informados, liste (copiando o texto exato) os que se aplicam aos
          riscos da obra, além dos cinco primeiros que sempre valem.
        """;

    private static readonly object SchemaEmergencia = new
    {
        type = "object",
        additionalProperties = false,
        required = new[] { "recomendados", "cenarios" },
        properties = new
        {
            recomendados = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[] { "id", "justificativa" },
                    properties = new
                    {
                        id = new { type = "string" },
                        justificativa = new { type = "string" },
                    },
                },
            },
            cenarios = new { type = "array", items = new { type = "string" } },
        },
    };

    public async Task<SugestaoEmergenciaIa> SugerirEmergenciaAsync(ContextoEmergenciaIa c, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Obra: {c.Obra}");
        sb.AppendLine($"Localização da obra: {c.Localizacao}");
        sb.AppendLine("Riscos da obra:");
        foreach (var r in c.Riscos) sb.AppendLine($"- {r}");
        if (c.Riscos.Count == 0) sb.AppendLine("(ainda sem riscos gerados)");
        sb.AppendLine("Candidatos (locais reais do mapa, com rota de carro a partir da obra):");
        foreach (var k in c.Candidatos)
            sb.AppendLine($"- id={k.Id} | tipo={k.Tipo} | nome={k.Nome} | atendeUrgencia={(k.AtendeUrgencia ? "true" : "false")} | {k.TempoMinutos} min | {k.DistanciaKm:0.0} km");
        sb.AppendLine("Cenários possíveis:");
        foreach (var n in c.CenariosPossiveis) sb.AppendLine($"- {n}");

        var r2 = await _chat.ObterAsync<RespostaEmergencia>(InstrucoesEmergencia, sb.ToString(), "emergencia_obra", SchemaEmergencia, ct, maxTokens: 3000, timeoutSegundos: 90);
        return new SugestaoEmergenciaIa(
            (r2.Recomendados ?? new()).Where(x => !string.IsNullOrWhiteSpace(x.Id))
                .Select(x => new RecomendacaoEmergenciaIa(x.Id!.Trim(), x.Justificativa?.Trim())).ToList(),
            (r2.Cenarios ?? new()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList());
    }

    private sealed record RespostaEmergencia(List<RecomendacaoModelo>? Recomendados, List<string>? Cenarios);
    private sealed record RecomendacaoModelo(string? Id, string? Justificativa);

    private sealed record RespostaEap(List<ItemEapModelo>? Itens);
    private sealed record ItemEapModelo(string? Codigo, string? Nome, int Nivel, string? Periodo, bool Usar, bool Conferir);
    private sealed record RespostaGhe(List<GheModelo>? Ghes);
    private sealed record GheModelo(string? Nome, string? Ambiente, string? Jornada, List<string>? Atividades, List<FuncaoModelo>? Funcoes);
    private sealed record FuncaoModelo(string? Nome, string? Descricao);
    private sealed record RespostaRiscos(List<RiscoModelo>? Riscos);
    private sealed record RiscoModelo(string? Tipo, string? Perigo, string? Danos, int Probabilidade, int Severidade, List<string>? Base, List<MedidaModelo>? Medidas);
    private sealed record MedidaModelo(string? Fase, string? Texto, string? Categoria);
}

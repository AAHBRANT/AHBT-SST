using System.Text;
using AAHBRANT.SST.Application.Ghes.AtividadesFuncao;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Infrastructure.Integracao.SuporteIa;

/// <summary>
/// IA (Claude, saída JSON estrita) que sugere quais atividades de um GHE cada função executa. Tudo é
/// sugestão: o servidor descarta função e código que não estavam na entrada, e quem confirma é o
/// Engenheiro de Segurança.
/// </summary>
public class ClaudeAnalistaAtividadesFuncao : IAnalistaAtividadesFuncao
{
    private const string Instrucoes = """
        Você é engenheiro de Segurança do Trabalho da AAHBRANT Engenharia. Em um PGR gerado pela EAP, as
        atividades de um GHE são etapas da obra; a Ordem de Serviço de cada trabalhador deve listar só as
        atividades que a função dele executa. Para cada função da lista, indique quais atividades da lista
        ela executa ou auxilia diretamente. Responda só com o JSON.
        - A descrição da função no PGR é a base da decisão. Marque a atividade quando a descrição (ou o
          ofício da função) mostrar que a função a executa ou auxilia diretamente.
        - NÃO marque uma etapa só porque ela acontece no mesmo ambiente ou no mesmo GHE.
        - Função de apoio ou de coordenação (ajudante, encarregado) marca só as atividades que a descrição cita.
        - justificativa: uma frase curta (até 200 caracteres) dizendo por que a função executa a atividade.
        - Responda pelo CÓDIGO da função (ex.: F01) e pelo código da atividade (ex.: A001), exatamente como
          na entrada. Não invente código nem função. Função sem nenhuma atividade devolve lista vazia.
        """;

    private static readonly object Schema = new
    {
        type = "object",
        additionalProperties = false,
        required = new[] { "funcoes" },
        properties = new
        {
            funcoes = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[] { "codigo", "atividades" },
                    properties = new
                    {
                        codigo = new { type = "string" },
                        atividades = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "object",
                                additionalProperties = false,
                                required = new[] { "codigo", "justificativa" },
                                properties = new
                                {
                                    codigo = new { type = "string" },
                                    justificativa = new { type = "string" },
                                },
                            },
                        },
                    },
                },
            },
        },
    };

    private readonly ClaudeChatJsonCliente _chat;
    private readonly ILogger<ClaudeAnalistaAtividadesFuncao> _logger;

    public ClaudeAnalistaAtividadesFuncao(ClaudeChatJsonCliente chat, ILogger<ClaudeAnalistaAtividadesFuncao> logger)
    {
        _chat = chat;
        _logger = logger;
    }

    private sealed class Resposta
    {
        public List<RespostaFuncao>? Funcoes { get; set; }
    }

    private sealed class RespostaFuncao
    {
        public string? Codigo { get; set; }
        public List<RespostaAtividade>? Atividades { get; set; }
    }

    private sealed class RespostaAtividade
    {
        public string? Codigo { get; set; }
        public string? Justificativa { get; set; }
    }

    public async Task<ResultadoAnaliseAtividadesFuncao> SugerirAsync(ContextoAtividadesFuncao contexto, CancellationToken ct)
    {
        var pedido = new StringBuilder();
        pedido.AppendLine($"GHE {contexto.GheNumero:00}");
        if (!string.IsNullOrWhiteSpace(contexto.Ambiente)) pedido.AppendLine($"Ambiente: {contexto.Ambiente!.Trim()}");
        pedido.AppendLine().AppendLine("FUNÇÕES (código — nome — descrição das atividades no PGR):");
        foreach (var f in contexto.Funcoes)
            pedido.AppendLine($"- {f.Codigo} — {f.Nome} — {(string.IsNullOrWhiteSpace(f.DescricaoNoPgr) ? "(sem descrição no PGR)" : f.DescricaoNoPgr!.Trim())}");
        pedido.AppendLine().AppendLine("ATIVIDADES (código — nome):");
        foreach (var a in contexto.Atividades) pedido.AppendLine($"- {a.Codigo} — {a.Nome}");

        var (r, tokensEntrada, tokensSaida) = await _chat.ObterComUsoAsync<Resposta>(
            Instrucoes, pedido.ToString(), "atividades_por_funcao", Schema, ct, maxTokens: 8000, timeoutSegundos: 120);
        _logger.LogInformation("IA atividades por função (GHE {Ghe}): {Funcoes} funções, {Atividades} atividades, {Entrada} tokens de entrada, {Saida} de saída",
            contexto.GheNumero, contexto.Funcoes.Count, contexto.Atividades.Count, tokensEntrada, tokensSaida);

        var funcoes = (r.Funcoes ?? new())
            .Select(f => new SugestaoFuncaoIa(f.Codigo,
                (f.Atividades ?? new()).Select(a => new SugestaoAtividadeIa(a.Codigo,
                    ClaudeChatJsonCliente.LimitarOuNulo(a.Justificativa, 300))).ToList()))
            .ToList();
        return new ResultadoAnaliseAtividadesFuncao(funcoes, tokensEntrada, tokensSaida);
    }
}

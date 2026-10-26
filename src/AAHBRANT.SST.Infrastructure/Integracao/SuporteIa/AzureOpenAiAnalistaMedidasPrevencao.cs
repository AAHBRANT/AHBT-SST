using System.Text;
using AAHBRANT.SST.Application.Riscos.Medidas;

namespace AAHBRANT.SST.Infrastructure.Integracao.SuporteIa;

/// <summary>
/// Separa os controles do PGR de um risco em medidas por fase (antes/durante/após) e sugere as que
/// faltam, via gpt5mini. A origem "PGR" declarada aqui é conferida depois no servidor
/// (ConciliacaoMedidasPgr): o modelo não decide o que entra validado.
/// </summary>
public class AzureOpenAiAnalistaMedidasPrevencao : IAnalistaMedidasPrevencao
{
    private const string Instrucoes = """
        Você é engenheiro de Segurança do Trabalho da AAHBRANT Engenharia. Vai preparar as medidas de
        prevenção de um risco do PGR para a Ordem de Serviço do trabalhador (NR-01 1.4.1 "c"),
        separadas pela fase do trabalho. Responda só com o JSON pedido.

        1) Medidas do PGR: transforme CADA controle do texto do PGR em uma medida, no imperativo e
           compreensível para o trabalhador (até 200 caracteres). origem "PGR" e em trechoPgr copie,
           LITERALMENTE e sem alterar nenhuma palavra, o trecho do texto do PGR de onde a medida saiu.
           Não omita nenhum controle do texto.
        2) Sugestões: acrescente só o que falta e é claramente necessário para este perigo e esta
           atividade (no máximo 4), seguindo a hierarquia da NR-01 (eliminação, proteção coletiva,
           administrativa, EPI por último) e os requisitos legais listados. origem "Sugestao" e
           trechoPgr null. Não repita o que já está no PGR. Não cite norma que não esteja na lista.
        3) fase: "Antes" (preparação, inspeção, liberação, PT/APR), "Durante" (execução) ou "Apos"
           (desmobilização, limpeza, guarda, registro).
        Nunca invente equipamento, procedimento interno ou nome de documento da empresa.
        """;

    private static readonly object Schema = new
    {
        type = "object",
        additionalProperties = false,
        required = new[] { "medidas" },
        properties = new
        {
            medidas = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[] { "fase", "texto", "origem", "trechoPgr" },
                    properties = new
                    {
                        fase = new { type = "string", @enum = new[] { "Antes", "Durante", "Apos" } },
                        texto = new { type = "string" },
                        origem = new { type = "string", @enum = new[] { "PGR", "Sugestao" } },
                        trechoPgr = new { type = new[] { "string", "null" } },
                    },
                },
            },
        },
    };

    private readonly ClaudeChatJsonCliente _chat;

    public AzureOpenAiAnalistaMedidasPrevencao(ClaudeChatJsonCliente chat) => _chat = chat;

    public async Task<IReadOnlyList<MedidaSugeridaIa>> SepararAsync(ContextoMedidasRisco c, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Atividade: {c.Atividade}");
        sb.AppendLine($"Perigo: {c.Perigo}");
        sb.AppendLine($"Consequência: {c.Consequencia ?? "não informada"}");
        sb.AppendLine($"Nível do risco: {c.Nivel}");
        sb.AppendLine();
        sb.AppendLine("Texto de controles do PGR:");
        sb.AppendLine(c.TextoPgr ?? "(o PGR não traz controles para este risco — use só sugestões)");
        sb.AppendLine();
        sb.AppendLine("Requisitos legais validados aplicáveis:");
        if (c.RequisitosLegais.Count == 0) sb.AppendLine("(nenhum)");
        foreach (var q in c.RequisitosLegais) sb.AppendLine($"- {q}");

        var r = await _chat.ObterAsync<RespostaModelo>(Instrucoes, sb.ToString(), "medidas_prevencao", Schema, ct);
        return (r.Medidas ?? new List<MedidaModelo>())
            .Select(m => new MedidaSugeridaIa(m.Fase, m.Texto, m.Origem, m.TrechoPgr))
            .ToList();
    }

    private sealed record RespostaModelo(List<MedidaModelo>? Medidas);

    private sealed record MedidaModelo(string? Fase, string? Texto, string? Origem, string? TrechoPgr);
}

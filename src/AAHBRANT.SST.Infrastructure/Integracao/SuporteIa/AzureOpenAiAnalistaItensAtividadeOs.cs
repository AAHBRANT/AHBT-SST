using System.Text;
using AAHBRANT.SST.Application.Atividades.DetalhesOs;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Infrastructure.Integracao.SuporteIa;

/// <summary>
/// Sugere, via gpt5mini, quando interromper a atividade, o que não pode ser feito sem capacitação ou
/// liberação e as autorizações exigidas, para a Ordem de Serviço. Tudo volta como sugestão: a SST
/// aceita, edita ou descarta cada item.
/// </summary>
public class AzureOpenAiAnalistaItensAtividadeOs : IAnalistaItensAtividadeOs
{
    private const string Instrucoes = """
        Você é engenheiro de Segurança do Trabalho da AAHBRANT Engenharia, preparando a Ordem de
        Serviço do trabalhador (NR-01 1.4.1 "c") para uma atividade de obra. Responda só com o JSON.

        Proponha, para esta atividade e estes riscos (no máximo 4 de cada tipo, só o que for claramente
        aplicável):
        - "InterromperQuando": situações em que o trabalhador deve parar a atividade e avisar a
          liderança (ex.: condição climática, falha de equipamento, ausência de proteção coletiva,
          risco grave e iminente). Frase curta, até 150 caracteres. requisito null.
        - "Restricao": parte da atividade que não pode ser executada sem capacitação, autorização ou
          liberação específica. texto = o que não pode ser feito; requisito = o que é exigido (ex.:
          "Treinamento NR-35 válido e PT emitida"). Até 150 caracteres cada.
        - "Autorizacao": liberações exigidas antes de começar (ex.: APR, PT, ASO apto para a
          atividade). requisito null.
        Linguagem simples, para o trabalhador entender. Não invente procedimento interno, nome de
        documento ou equipamento que não esteja no contexto.
        """;

    private static readonly object Schema = new
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
                    required = new[] { "tipo", "texto", "requisito" },
                    properties = new
                    {
                        tipo = new { type = "string", @enum = new[] { "InterromperQuando", "Restricao", "Autorizacao" } },
                        texto = new { type = "string" },
                        requisito = new { type = new[] { "string", "null" } },
                    },
                },
            },
        },
    };

    private readonly ClaudeChatJsonCliente _chat;

    public AzureOpenAiAnalistaItensAtividadeOs(ClaudeChatJsonCliente chat) => _chat = chat;

    public async Task<IReadOnlyList<ItemSugeridoIa>> SugerirAsync(ContextoItensAtividadeOs c, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Atividade: {c.Atividade}");
        sb.AppendLine($"Condições: {c.Condicoes ?? "não informadas"}");
        sb.AppendLine($"Atividades críticas do GHE: {c.AtividadesCriticas ?? "não informadas"}");
        sb.AppendLine("Riscos do PGR:");
        foreach (var r in c.Riscos) sb.AppendLine($"- {r}");

        var r2 = await _chat.ObterAsync<RespostaModelo>(Instrucoes, sb.ToString(), "itens_os_atividade", Schema, ct);
        return (r2.Itens ?? new List<ItemModelo>())
            .Select(i => new ItemSugeridoIa(
                Enum.TryParse<TipoItemAtividadeOs>(i.Tipo, out var tipo) ? tipo : null,
                i.Texto,
                i.Requisito))
            .ToList();
    }

    private sealed record RespostaModelo(List<ItemModelo>? Itens);

    private sealed record ItemModelo(string? Tipo, string? Texto, string? Requisito);
}

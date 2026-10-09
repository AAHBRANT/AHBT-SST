using AAHBRANT.SST.Application.SuporteIa;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Infrastructure.Integracao.SuporteIa;

/// <summary>
/// Classificação do relato do Suporte IA via gpt5mini, com saída estruturada (json_schema strict)
/// — o modelo só pode devolver valores válidos dos enums.
/// </summary>
public class AzureOpenAiClassificadorRelato : IClassificadorRelatoSuporteIa
{
    private const string InstrucoesSistema = """
        Você classifica relatos de usuários do sistema SST (Segurança e Saúde do Trabalho) da AAHBRANT
        para abrir um chamado de suporte. O relato normalmente foi falado e transcrito, então pode ter
        hesitações e repetições. Responda só com o JSON pedido.
        - tipo: Erro (algo quebrado ou com mensagem de erro), Duvida (como usar alguma coisa),
          Melhoria (pedido de mudança ou funcionalidade nova).
        - severidade: Baixa (incômodo pequeno), Media (atrapalha mas há alternativa), Alta (impede uma
          tarefa importante), Critica (para o trabalho em obra ou gera risco legal/de segurança).
        - titulo: até 80 caracteres, direto, como o usuário diria.
        - modulo: tela ou módulo citado (ex.: Inspeções, EPI, EPC, Uniforme, DDS, APR, PT,
          Treinamentos, Funcionários, Obras, Acidentes, CIPA, PGR) ou null se não der para saber.
        - descricao: o relato reescrito em texto claro e organizado, em português, sem inventar fatos
          e sem remover detalhes do que o usuário disse.
        """;

    private static readonly object Schema = new
    {
        type = "object",
        additionalProperties = false,
        required = new[] { "tipo", "severidade", "titulo", "modulo", "descricao" },
        properties = new
        {
            tipo = new { type = "string", @enum = new[] { "Erro", "Duvida", "Melhoria" } },
            severidade = new { type = "string", @enum = new[] { "Baixa", "Media", "Alta", "Critica" } },
            titulo = new { type = "string" },
            modulo = new { type = new[] { "string", "null" } },
            descricao = new { type = "string" },
        },
    };

    private readonly AzureOpenAiChatJsonCliente _chat;

    public AzureOpenAiClassificadorRelato(AzureOpenAiChatJsonCliente chat) => _chat = chat;

    public async Task<ChamadoSugeridoSuporteIa> ClassificarAsync(string relato, CancellationToken ct)
    {
        var sugestao = await _chat.ObterAsync<RespostaModelo>(InstrucoesSistema, relato, "chamado_suporte", Schema, ct);

        // Mesmos limites do CriarSolicitacaoSuporteIaCommandValidator.
        return new ChamadoSugeridoSuporteIa(
            Enum.TryParse<TipoSolicitacaoSuporteIa>(sugestao.Tipo, out var tipo) ? tipo : TipoSolicitacaoSuporteIa.Duvida,
            Enum.TryParse<SeveridadeSolicitacaoSuporteIa>(sugestao.Severidade, out var severidade) ? severidade : SeveridadeSolicitacaoSuporteIa.Media,
            AzureOpenAiChatJsonCliente.Limitar(sugestao.Titulo, 180),
            AzureOpenAiChatJsonCliente.LimitarOuNulo(sugestao.Modulo, 120),
            AzureOpenAiChatJsonCliente.Limitar(sugestao.Descricao, 4000));
    }

    private sealed record RespostaModelo(string? Tipo, string? Severidade, string? Titulo, string? Modulo, string? Descricao);
}

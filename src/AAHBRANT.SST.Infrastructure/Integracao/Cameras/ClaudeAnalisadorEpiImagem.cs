using AAHBRANT.SST.Application.Cameras;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Integracao.SuporteIa;

namespace AAHBRANT.SST.Infrastructure.Integracao.Cameras;

public class ClaudeAnalisadorEpiImagem : IAnalisadorEpiImagem
{
    private const string Instrucoes =
        "Você analisa fotos de canteiro de obras para segurança do trabalho. " +
        "Conte apenas pessoas claramente visíveis na imagem. " +
        "Para cada pessoa, verifique: (1) capacete de segurança na cabeça; boné, touca, capuz ou chapéu não contam como capacete; " +
        "(2) colete refletivo ou de alta visibilidade sobre o tronco. " +
        "Se a pessoa estiver longe demais, encoberta ou de um ângulo em que não dá para ter certeza, considere que ela está usando o item e reduza a confiança. " +
        "Não identifique pessoas nem descreva rostos. Responda somente no formato solicitado.";

    private const string Pedido =
        "Informe: pessoas (total de pessoas visíveis), semCapacete (quantas sem capacete), " +
        "semColete (quantas sem colete) e confianca (alta, media ou baixa) da sua avaliação.";

    private static readonly object Schema = new
    {
        type = "object",
        properties = new
        {
            pessoas = new { type = "integer" },
            semCapacete = new { type = "integer" },
            semColete = new { type = "integer" },
            confianca = new { type = "string", @enum = new[] { "alta", "media", "baixa" } },
        },
        required = new[] { "pessoas", "semCapacete", "semColete", "confianca" },
        additionalProperties = false,
    };

    private sealed record Resposta(int Pessoas, int SemCapacete, int SemColete, string? Confianca);

    private readonly ClaudeChatJsonCliente _cliente;
    public ClaudeAnalisadorEpiImagem(ClaudeChatJsonCliente cliente) => _cliente = cliente;

    public async Task<ResultadoAnaliseEpi> AnalisarAsync(byte[] imagemJpeg, CancellationToken ct)
    {
        var imagens = new[] { $"data:image/jpeg;base64,{Convert.ToBase64String(imagemJpeg)}" };
        var r = await _cliente.ObterAsync<Resposta>(Instrucoes, Pedido, "analise_epi", Schema, ct,
            maxTokens: 500, timeoutSegundos: 60, imagens: imagens);

        if (r.Pessoas < 0 || r.SemCapacete < 0 || r.SemColete < 0 || r.SemCapacete > r.Pessoas || r.SemColete > r.Pessoas)
            throw new InvalidOperationException("A IA devolveu contagens incoerentes.");

        var confianca = r.Confianca?.ToLowerInvariant() switch
        {
            "alta" => ConfiancaAnaliseEpi.Alta,
            "media" => ConfiancaAnaliseEpi.Media,
            _ => ConfiancaAnaliseEpi.Baixa,
        };
        return new ResultadoAnaliseEpi(r.Pessoas, r.SemCapacete, r.SemColete, confianca);
    }
}

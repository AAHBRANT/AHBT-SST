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
        "semColete (quantas sem colete) e confianca (alta, media ou baixa) da sua avaliação. " +
        "Em pessoasDetalhe, inclua uma entrada para cada pessoa contada, com a posição aproximada do centro da cabeça " +
        "(cabecaX de 0 na borda esquerda a 1 na borda direita; cabecaY de 0 no topo a 1 na base da imagem) " +
        "e se ela está semCapacete e semColete. A quantidade de entradas e os totais precisam bater.";

    private static readonly object Schema = new
    {
        type = "object",
        properties = new
        {
            pessoas = new { type = "integer" },
            semCapacete = new { type = "integer" },
            semColete = new { type = "integer" },
            confianca = new { type = "string", @enum = new[] { "alta", "media", "baixa" } },
            pessoasDetalhe = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        cabecaX = new { type = "number" },
                        cabecaY = new { type = "number" },
                        semCapacete = new { type = "boolean" },
                        semColete = new { type = "boolean" },
                    },
                    required = new[] { "cabecaX", "cabecaY", "semCapacete", "semColete" },
                    additionalProperties = false,
                },
            },
        },
        required = new[] { "pessoas", "semCapacete", "semColete", "confianca", "pessoasDetalhe" },
        additionalProperties = false,
    };

    private sealed record PessoaResposta(double CabecaX, double CabecaY, bool SemCapacete, bool SemColete);
    private sealed record Resposta(int Pessoas, int SemCapacete, int SemColete, string? Confianca, List<PessoaResposta>? PessoasDetalhe = null);

    private readonly ClaudeChatJsonCliente _cliente;
    public ClaudeAnalisadorEpiImagem(ClaudeChatJsonCliente cliente) => _cliente = cliente;

    public async Task<ResultadoAnaliseEpi> AnalisarAsync(byte[] imagemJpeg, CancellationToken ct)
    {
        var imagens = new[] { $"data:image/jpeg;base64,{Convert.ToBase64String(imagemJpeg)}" };
        var r = await _cliente.ObterAsync<Resposta>(Instrucoes, Pedido, "analise_epi", Schema, ct,
            maxTokens: 1500, timeoutSegundos: 60, imagens: imagens, modelo: _cliente.ModeloCameras);

        if (r.Pessoas < 0 || r.SemCapacete < 0 || r.SemColete < 0 || r.SemCapacete > r.Pessoas || r.SemColete > r.Pessoas)
            throw new InvalidOperationException("A IA devolveu contagens incoerentes.");

        var confianca = r.Confianca?.ToLowerInvariant() switch
        {
            "alta" => ConfiancaAnaliseEpi.Alta,
            "media" => ConfiancaAnaliseEpi.Media,
            _ => ConfiancaAnaliseEpi.Baixa,
        };
        return new ResultadoAnaliseEpi(r.Pessoas, r.SemCapacete, r.SemColete, confianca, DetalheCoerente(r));
    }

    // As posições só valem se baterem com a contagem; senão a cena fica sem identificação por rosto
    // (as contagens continuam valendo para a ocorrência e o alerta).
    private static IReadOnlyList<PessoaNaCena>? DetalheCoerente(Resposta r)
    {
        var detalhe = r.PessoasDetalhe;
        if (detalhe is null || detalhe.Count != r.Pessoas
            || detalhe.Count(p => p.SemCapacete) != r.SemCapacete || detalhe.Count(p => p.SemColete) != r.SemColete
            || detalhe.Any(p => double.IsNaN(p.CabecaX) || double.IsNaN(p.CabecaY)))
            return null;
        return detalhe
            .Select(p => new PessoaNaCena(Math.Clamp(p.CabecaX, 0, 1), Math.Clamp(p.CabecaY, 0, 1), p.SemCapacete, p.SemColete))
            .ToList();
    }
}

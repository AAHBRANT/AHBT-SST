using AAHBRANT.SST.Application.Assinatura;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Application.Trabalhadores.Commands;

// Chamado depois de salvar uma troca de obra (edição do trabalhador ou sincronização do G-RH): leva o
// cadastro facial para o grupo da nova obra. A troca de obra já está gravada, então uma falha no Azure
// não desfaz nada nem derruba a requisição — fica no log, e "Refazer cadastro facial" resolve na mão.
internal static class RealocacaoCadastroFacial
{
    public static async Task TentarAsync(IAutenticacaoFacialService facial, ILogger logger, Guid trabalhadorId, Guid obraAnteriorId, CancellationToken ct)
    {
        try
        {
            await facial.RealocarCadastroAsync(trabalhadorId, obraAnteriorId, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Não foi possível levar o cadastro facial do trabalhador {TrabalhadorId} para a nova obra.", trabalhadorId);
        }
    }
}

using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Riscos;

// Resolve o NivelRisco a partir da MatrizRiscoConfig (§36 da Base de Conhecimento):
// modelo único e global (não há mais matriz por empresa).
public static class NivelRiscoLookup
{
    public static async Task<NivelRisco> ResolverAsync(IAppDbContext db, Guid atividadeId, int probabilidade, int severidade, CancellationToken ct)
    {
        var config = await db.MatrizRiscoConfigs
            .Include(c => c.Celulas)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Nenhuma MatrizRiscoConfig cadastrada.");

        var celula = config.Celulas.FirstOrDefault(c => c.Probabilidade == probabilidade && c.Severidade == severidade)
            ?? throw new InvalidOperationException($"A matriz de risco '{config.Nome}' não tem célula para Probabilidade={probabilidade}/Severidade={severidade}.");

        return celula.NivelRisco;
    }
}

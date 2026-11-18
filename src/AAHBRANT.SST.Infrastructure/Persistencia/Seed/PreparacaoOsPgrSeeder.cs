using AAHBRANT.SST.Application.GeracaoPgr;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Seed;

// Garante que todas as obras que já possuem PGR vigente tenham as Instruções de Trabalho (IT)
// vigentes geradas/vinculadas às atividades e as atividades do GHE confirmadas para as funções,
// destravando a emissão da Ordem de Serviço sem exigir retrabalho manual.
public static class PreparacaoOsPgrSeeder
{
    public static async Task ExecutarAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SstDbContext>();

        var obraIds = await db.Pgrs
            .Where(p => p.Status == StatusPgr.Vigente)
            .Select(p => p.ObraId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var obraId in obraIds)
        {
            await PreparacaoAutomaticaOsDoPgr.GarantirParaObraComPgrVigenteAsync(db, obraId, ct);
        }
    }
}

using AAHBRANT.SST.Application.Cameras.Commands;
using AAHBRANT.SST.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cameras;

// A miniatura do "Testar conexão" (foto de pessoa possível) não tem job de limpeza: quem toca nos testes do agente
// (novo pedido, consulta do resultado, consulta de configuração do agente) apaga as que passaram da retenção.
internal static class MiniaturasTesteConexao
{
    public static async Task LimparAntigasAsync(IAppDbContext db, Guid agenteId, DateTime agora, CancellationToken ct)
    {
        var corte = agora - SolicitarTesteConexaoCameraCommandHandler.RetencaoDaMiniatura;
        // Sem filtro global: vale também na rota anônima do agente (escopo definido só depois do segredo), com o predicado explícito.
        var antigos = await db.TestesConexaoCamera.IgnoreQueryFilters()
            .Where(t => t.AgenteCameraId == agenteId && t.Miniatura != null && t.SolicitadoEmUtc < corte)
            .ToListAsync(ct);
        foreach (var t in antigos) t.Miniatura = null;
        if (antigos.Count > 0) await db.SaveChangesAsync(ct);
    }
}

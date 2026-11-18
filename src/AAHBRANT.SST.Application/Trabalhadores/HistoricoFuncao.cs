using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Trabalhadores;

// Ponto único do histórico de função: quem troca a função registra por aqui, e quem precisa saber
// "qual era a função em tal data" (ficha de EPI, assinatura da OS) pergunta aqui.
public static class HistoricoFuncao
{
    // Só adiciona ao contexto: grava junto com o SaveChanges de quem trocou a função, para a troca
    // e o histórico entrarem (ou falharem) juntos.
    public static void Registrar(IAppDbContext db, Guid trabalhadorId, Guid funcaoAnteriorId, Guid funcaoNovaId)
    {
        if (funcaoAnteriorId == funcaoNovaId) return;

        db.HistoricosFuncaoTrabalhador.Add(new HistoricoFuncaoTrabalhador
        {
            TrabalhadorId = trabalhadorId,
            FuncaoAnteriorId = funcaoAnteriorId,
            FuncaoNovaId = funcaoNovaId,
            AlteradaEm = DateTime.UtcNow,
        });
    }

    public record Troca(Guid TrabalhadorId, Guid FuncaoAnteriorId, DateTime AlteradaEm);

    public static async Task<List<Troca>> CarregarAsync(
        IAppDbContext db, IReadOnlyCollection<Guid> trabalhadorIds, CancellationToken ct) =>
        await db.HistoricosFuncaoTrabalhador.AsNoTracking()
            .Where(h => trabalhadorIds.Contains(h.TrabalhadorId))
            .OrderBy(h => h.AlteradaEm)
            .Select(h => new Troca(h.TrabalhadorId, h.FuncaoAnteriorId, h.AlteradaEm))
            .ToListAsync(ct);

    // Função que o trabalhador exercia em "data": a anterior da primeira troca feita depois dela;
    // sem troca posterior, é a função atual. Trocas feitas antes de este histórico existir não têm
    // registro — para esse período a resposta continua sendo a função atual, como era antes.
    public static Guid FuncaoNaData(IEnumerable<Troca> trocas, Guid trabalhadorId, Guid funcaoAtualId, DateTime data)
    {
        var posterior = trocas
            .Where(t => t.TrabalhadorId == trabalhadorId && t.AlteradaEm > data)
            .OrderBy(t => t.AlteradaEm)
            .FirstOrDefault();
        return posterior?.FuncaoAnteriorId ?? funcaoAtualId;
    }
}

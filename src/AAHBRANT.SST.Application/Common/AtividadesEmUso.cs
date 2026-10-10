using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;

namespace AAHBRANT.SST.Application.Common;

// Atividade de GHE desativado (estrutura de uma revisão anterior do PGR, substituída pela leitura com IA
// em 10/10/2026) continua existindo — APR/PT/NC já emitidas apontam para ela —, mas sai das listas de
// escolha e do inventário. Ghes tem filtro de Ativo, então "GHE não encontrado" = GHE desativado.
public static class AtividadesEmUso
{
    public static IQueryable<Atividade> EmUso(this IQueryable<Atividade> query, IAppDbContext db) =>
        query.Where(a => a.GheId == null || db.Ghes.Any(g => g.Id == a.GheId));
}

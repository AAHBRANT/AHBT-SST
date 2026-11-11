using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds;

// Grava a presença já preparada no contexto (participante adicionado, pessoa tirada da seleção). Com
// vários tablets no mesmo DDS, a mesma pessoa pode ser confirmada em dois deles no mesmo instante: as
// duas requisições passam pela checagem de "já participa" e só o índice único (DdsId, TrabalhadorId)
// segura a segunda. Devolve false quando foi a outra requisição que gravou a presença.
public static class PresencaDdsUnica
{
    public static async Task<bool> GravarAsync(
        IAppDbContext db, DdsParticipante participante, IEnumerable<DdsFuncionarioSelecionado> selecaoRemovida, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Nada desta tentativa foi gravado (uma transação só). Tira tudo do rastreamento para uma
            // gravação seguinte no mesmo contexto não repetir a inserção recusada.
            db.DdsParticipantes.Entry(participante).State = EntityState.Detached;
            foreach (var item in selecaoRemovida)
                db.DdsFuncionariosSelecionados.Entry(item).State = EntityState.Detached;

            if (await db.DdsParticipantes.AnyAsync(p => p.DdsId == participante.DdsId && p.TrabalhadorId == participante.TrabalhadorId, ct))
                return false;
            throw;
        }
    }
}

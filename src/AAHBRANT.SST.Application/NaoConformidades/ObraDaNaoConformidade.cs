using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.NaoConformidades;

// Obra da NC: a informada na tela; senão a da atividade; senão a da atividade do risco. Sem nenhuma, a NC é
// recusada (a NC precisa de obra para o escopo e para a integração com o G-QUALI). A obra precisa estar no
// escopo de quem registra.
public static class ObraDaNaoConformidade
{
    public const string MensagemObrigatoria = "Informe a obra da não conformidade.";

    public static async Task<Guid> ResolverAsync(IAppDbContext db, Guid? obraId, Guid? atividadeId, Guid? riscoId, CancellationToken ct)
    {
        var obra = obraId;
        if (obra is null && atividadeId is { } a)
            obra = await db.Atividades.IgnoreQueryFilters().Where(x => x.Id == a).Select(x => (Guid?)x.ObraId).FirstOrDefaultAsync(ct);
        if (obra is null && riscoId is { } r)
            obra = await db.Riscos.IgnoreQueryFilters().Where(x => x.Id == r)
                .Join(db.Atividades.IgnoreQueryFilters(), x => x.AtividadeId, at => at.Id, (x, at) => (Guid?)at.ObraId)
                .FirstOrDefaultAsync(ct);
        if (obra is null)
            throw new ValidationException([new ValidationFailure("ObraId", MensagemObrigatoria)]);
        if (!await db.Obras.AnyAsync(o => o.Id == obra, ct))
            throw new KeyNotFoundException($"Obra {obra} não encontrada.");
        if (!db.ObraNoEscopo(obra))
            throw new KeyNotFoundException($"Obra {obra} não encontrada.");
        return obra.Value;
    }
}

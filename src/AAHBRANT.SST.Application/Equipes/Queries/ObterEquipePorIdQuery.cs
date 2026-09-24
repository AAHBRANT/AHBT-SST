using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Equipes.Queries;

public record ObterEquipePorIdQuery(Guid Id) : IRequest<EquipeDto?>;

public class ObterEquipePorIdQueryHandler : IRequestHandler<ObterEquipePorIdQuery, EquipeDto?>
{
    private readonly IAppDbContext _db;

    public ObterEquipePorIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<EquipeDto?> Handle(ObterEquipePorIdQuery request, CancellationToken ct)
    {
        // Setor e obra fora da projeção (ver ListarEquipesQuery para o porquê): aqui o estrago era
        // ainda maior que na lista — com o setor excluído, o INNER JOIN não devolvia linha nenhuma
        // e abrir a equipe dava 404, como se ela não existisse.
        var equipe = await _db.Equipes
            .Where(e => e.Id == request.Id)
            .Select(e => new
            {
                e.Id,
                e.SetorId,
                e.Nome,
                e.EncarregadoId,
                EncarregadoNome = e.Encarregado != null ? e.Encarregado.Nome : null,
                QuantidadeTrabalhadores = e.Trabalhadores.Count(t => t.Ativo),
            })
            .FirstOrDefaultAsync(ct);

        if (equipe is null) return null;

        var setor = await _db.Setores.AsNoTracking().IgnoreQueryFilters()
            .Where(s => s.Id == equipe.SetorId)
            .Select(s => new { s.Nome, s.ObraId, s.Ativo })
            .FirstOrDefaultAsync(ct);

        var obraNome = setor is null
            ? null
            : await _db.Obras.AsNoTracking().IgnoreQueryFilters()
                .Where(o => o.Id == setor.ObraId)
                .Select(o => o.Ativo ? o.Nome : o.Nome + " (obra removida)")
                .FirstOrDefaultAsync(ct);

        return new EquipeDto
        {
            Id = equipe.Id,
            SetorId = equipe.SetorId,
            SetorNome = setor is null ? "Setor removido" : setor.Ativo ? setor.Nome : $"{setor.Nome} (setor removido)",
            ObraId = setor?.ObraId ?? Guid.Empty,
            ObraNome = obraNome ?? "Obra removida",
            Nome = equipe.Nome,
            EncarregadoId = equipe.EncarregadoId,
            EncarregadoNome = equipe.EncarregadoNome,
            QuantidadeTrabalhadores = equipe.QuantidadeTrabalhadores,
        };
    }
}

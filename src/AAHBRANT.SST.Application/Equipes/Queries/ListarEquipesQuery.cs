using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Equipes.Queries;

public record ListarEquipesQuery(Guid? ObraId, Guid? SetorId) : IRequest<List<EquipeDto>>;

public class ListarEquipesQueryHandler : IRequestHandler<ListarEquipesQuery, List<EquipeDto>>
{
    private readonly IAppDbContext _db;

    public ListarEquipesQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<EquipeDto>> Handle(ListarEquipesQuery request, CancellationToken ct)
    {
        var query = _db.Equipes.AsQueryable();
        if (request.SetorId.HasValue)
        {
            query = query.Where(e => e.SetorId == request.SetorId.Value);
        }
        else if (request.ObraId.HasValue)
        {
            query = query.Where(e => e.Setor!.ObraId == request.ObraId.Value);
        }

        return await query
            .OrderBy(e => e.Nome)
            .Select(e => new EquipeDto
            {
                Id = e.Id,
                SetorId = e.SetorId,
                SetorNome = e.Setor!.Nome,
                ObraId = e.Setor.ObraId,
                ObraNome = e.Setor.Obra!.Nome,
                Nome = e.Nome,
                EncarregadoId = e.EncarregadoId,
                EncarregadoNome = e.Encarregado != null ? e.Encarregado.Nome : null,
                QuantidadeTrabalhadores = e.Trabalhadores.Count(t => t.Ativo)
            })
            .ToListAsync(ct);
    }
}

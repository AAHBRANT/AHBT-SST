using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.ChecklistModelos.Queries;

public record ListarChecklistModelosQuery(TipoInspecao? TipoInspecao = null) : IRequest<List<ChecklistModeloDto>>;

public class ListarChecklistModelosQueryHandler : IRequestHandler<ListarChecklistModelosQuery, List<ChecklistModeloDto>>
{
    private readonly IAppDbContext _db;

    public ListarChecklistModelosQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<ChecklistModeloDto>> Handle(ListarChecklistModelosQuery request, CancellationToken ct)
    {
        var query = _db.ChecklistModelos.Include(c => c.Itens).AsQueryable();

        if (request.TipoInspecao.HasValue)
            query = query.Where(c => c.TipoInspecao == request.TipoInspecao.Value);

        var checklists = await query.OrderByDescending(c => c.CreatedAtUtc).ToListAsync(ct);

        return checklists.Select(c => new ChecklistModeloDto
        {
            Id = c.Id,
            Nome = c.Nome,
            TipoInspecao = c.TipoInspecao,
            Versao = c.Versao,
            ChecklistModeloAnteriorId = c.ChecklistModeloAnteriorId,
            QuantidadeItens = c.Itens.Count(i => i.Ativo)
        }).ToList();
    }
}

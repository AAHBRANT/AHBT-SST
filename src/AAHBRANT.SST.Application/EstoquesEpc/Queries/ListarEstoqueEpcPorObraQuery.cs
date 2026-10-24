using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EstoquesEpc.Queries;

public record ListarEstoqueEpcPorObraQuery(Guid ObraId) : IRequest<List<EstoqueEpcPorObraDto>>;

public class ListarEstoqueEpcPorObraQueryHandler : IRequestHandler<ListarEstoqueEpcPorObraQuery, List<EstoqueEpcPorObraDto>>
{
    private readonly IAppDbContext _db;
    public ListarEstoqueEpcPorObraQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<EstoqueEpcPorObraDto>> Handle(ListarEstoqueEpcPorObraQuery request, CancellationToken ct)
        => await _db.CatalogoEpcs
            .Where(c => c.Ativo)
            .OrderBy(c => c.Nome)
            .Select(c => new EstoqueEpcPorObraDto(
                c.Id,
                c.Nome,
                c.Fabricante,
                c.Estoques.Where(e => e.ObraId == request.ObraId).Sum(e => (int?)e.Saldo) ?? 0))
            .ToListAsync(ct);
}

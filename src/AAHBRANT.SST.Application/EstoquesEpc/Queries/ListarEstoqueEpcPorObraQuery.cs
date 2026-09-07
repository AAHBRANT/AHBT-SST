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
        => await _db.EstoquesEpc
            .Where(e => e.ObraId == request.ObraId)
            .OrderBy(e => e.CatalogoEpc!.Nome)
            .Select(e => new EstoqueEpcPorObraDto(e.CatalogoEpcId, e.CatalogoEpc!.Nome, e.Saldo))
            .ToListAsync(ct);
}

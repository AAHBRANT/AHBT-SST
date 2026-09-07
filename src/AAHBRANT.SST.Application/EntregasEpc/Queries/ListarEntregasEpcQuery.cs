using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EntregasEpc.Queries;

public record ListarEntregasEpcQuery(Guid? TrabalhadorId) : IRequest<List<EntregaEpcDto>>;

public class ListarEntregasEpcQueryHandler : IRequestHandler<ListarEntregasEpcQuery, List<EntregaEpcDto>>
{
    private readonly IAppDbContext _db;
    public ListarEntregasEpcQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<EntregaEpcDto>> Handle(ListarEntregasEpcQuery request, CancellationToken ct)
        => await _db.EntregasEpc
            .Where(e => request.TrabalhadorId == null || e.TrabalhadorId == request.TrabalhadorId)
            .OrderByDescending(e => e.DataEntrega)
            .Select(e => new EntregaEpcDto(e.Id, e.TrabalhadorId, e.CatalogoEpcId, e.Quantidade, e.DataEntrega, e.MotivoTipo, e.Observacoes))
            .ToListAsync(ct);
}

using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EntregasEpc.Queries;

public record ObterEntregaEpcPorIdQuery(Guid Id) : IRequest<EntregaEpcDto?>;

public class ObterEntregaEpcPorIdQueryHandler : IRequestHandler<ObterEntregaEpcPorIdQuery, EntregaEpcDto?>
{
    private readonly IAppDbContext _db;
    public ObterEntregaEpcPorIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<EntregaEpcDto?> Handle(ObterEntregaEpcPorIdQuery request, CancellationToken ct)
        => await _db.EntregasEpc
            .Where(e => e.Id == request.Id)
            .Select(e => new EntregaEpcDto(e.Id, e.TrabalhadorId, e.CatalogoEpcId, e.Quantidade, e.DataEntrega, e.MotivoTipo, e.Observacoes))
            .FirstOrDefaultAsync(ct);
}

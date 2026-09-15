using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EntregasUniforme.Queries;

public record ListarEntregasUniformeQuery(Guid? TrabalhadorId) : IRequest<List<EntregaUniformeDto>>;

public class ListarEntregasUniformeQueryHandler : IRequestHandler<ListarEntregasUniformeQuery, List<EntregaUniformeDto>>
{
    private readonly IAppDbContext _db;
    public ListarEntregasUniformeQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<EntregaUniformeDto>> Handle(ListarEntregasUniformeQuery request, CancellationToken ct)
        => await _db.EntregasUniforme
            .Where(e => request.TrabalhadorId == null || e.TrabalhadorId == request.TrabalhadorId)
            .OrderByDescending(e => e.DataEntrega)
            .Select(e => new EntregaUniformeDto(e.Id, e.TrabalhadorId, e.CatalogoUniformeId, e.Tamanho, e.Quantidade, e.DataEntrega, e.MotivoTipo, e.Observacoes))
            .ToListAsync(ct);
}

using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EntregasUniforme.Queries;

public record ObterEntregaUniformePorIdQuery(Guid Id) : IRequest<EntregaUniformeDto?>;

public class ObterEntregaUniformePorIdQueryHandler : IRequestHandler<ObterEntregaUniformePorIdQuery, EntregaUniformeDto?>
{
    private readonly IAppDbContext _db;
    public ObterEntregaUniformePorIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<EntregaUniformeDto?> Handle(ObterEntregaUniformePorIdQuery request, CancellationToken ct)
        => await _db.EntregasUniforme
            .Where(e => e.Id == request.Id)
            .Select(e => new EntregaUniformeDto(e.Id, e.TrabalhadorId, e.CatalogoUniformeId, e.Tamanho, e.Quantidade, e.DataEntrega, e.MotivoTipo, e.Observacoes))
            .FirstOrDefaultAsync(ct);
}

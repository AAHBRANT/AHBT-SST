using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CatalogosUniforme.Queries;

public record ObterCatalogoUniformePorIdQuery(Guid Id) : IRequest<CatalogoUniformeDto?>;

public class ObterCatalogoUniformePorIdQueryHandler : IRequestHandler<ObterCatalogoUniformePorIdQuery, CatalogoUniformeDto?>
{
    private readonly IAppDbContext _db;
    public ObterCatalogoUniformePorIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<CatalogoUniformeDto?> Handle(ObterCatalogoUniformePorIdQuery request, CancellationToken ct)
        => await _db.CatalogoUniformes
            .Where(x => x.Id == request.Id)
            .Select(x => new CatalogoUniformeDto(x.Id, x.Nome, x.Categoria))
            .FirstOrDefaultAsync(ct);
}

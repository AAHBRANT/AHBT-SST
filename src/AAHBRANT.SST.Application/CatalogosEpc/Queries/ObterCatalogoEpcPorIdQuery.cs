using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CatalogosEpc.Queries;

public record ObterCatalogoEpcPorIdQuery(Guid Id) : IRequest<CatalogoEpcDto?>;

public class ObterCatalogoEpcPorIdQueryHandler : IRequestHandler<ObterCatalogoEpcPorIdQuery, CatalogoEpcDto?>
{
    private readonly IAppDbContext _db;
    public ObterCatalogoEpcPorIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<CatalogoEpcDto?> Handle(ObterCatalogoEpcPorIdQuery request, CancellationToken ct)
        => await _db.CatalogoEpcs
            .Where(x => x.Id == request.Id)
            .Select(x => new CatalogoEpcDto(x.Id, x.Nome, x.Categoria, x.FotoConteudo != null))
            .FirstOrDefaultAsync(ct);
}

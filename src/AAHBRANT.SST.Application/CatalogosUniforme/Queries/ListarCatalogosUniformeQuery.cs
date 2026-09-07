using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CatalogosUniforme.Queries;

public record ListarCatalogosUniformeQuery : IRequest<List<CatalogoUniformeDto>>;

public class ListarCatalogosUniformeQueryHandler : IRequestHandler<ListarCatalogosUniformeQuery, List<CatalogoUniformeDto>>
{
    private readonly IAppDbContext _db;
    public ListarCatalogosUniformeQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<CatalogoUniformeDto>> Handle(ListarCatalogosUniformeQuery request, CancellationToken ct)
        => await _db.CatalogoUniformes
            .OrderBy(x => x.Nome)
            .Select(x => new CatalogoUniformeDto(x.Id, x.Nome, x.Categoria, x.FotoConteudo != null))
            .ToListAsync(ct);
}

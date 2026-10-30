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
            .Select(x => new CatalogoUniformeDto(x.Id, x.Nome, x.Categoria, x.FotoConteudo != null || x.ReferenciaVisual != null, x.ReferenciaVisual, _db.MatrizUniformeFuncoes.Where(m => m.CatalogoUniformeId == x.Id).Select(m => m.Funcao!.Nome).OrderBy(n => n).ToList()))
            .ToListAsync(ct);
}

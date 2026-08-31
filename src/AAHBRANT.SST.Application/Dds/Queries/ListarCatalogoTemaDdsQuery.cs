using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Queries;

public record ListarCatalogoTemaDdsQuery : IRequest<List<CatalogoTemaDdsDto>>;

public class ListarCatalogoTemaDdsQueryHandler : IRequestHandler<ListarCatalogoTemaDdsQuery, List<CatalogoTemaDdsDto>>
{
    private readonly IAppDbContext _db;

    public ListarCatalogoTemaDdsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<CatalogoTemaDdsDto>> Handle(ListarCatalogoTemaDdsQuery request, CancellationToken ct)
    {
        return await _db.CatalogosTemaDds
            .OrderBy(c => c.Nome)
            .Select(c => new CatalogoTemaDdsDto { Id = c.Id, Nome = c.Nome, Descricao = c.Descricao })
            .ToListAsync(ct);
    }
}

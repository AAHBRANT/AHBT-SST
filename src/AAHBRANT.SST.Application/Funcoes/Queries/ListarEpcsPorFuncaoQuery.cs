using AAHBRANT.SST.Application.CatalogosEpc;
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Funcoes.Queries;

public record ListarEpcsPorFuncaoQuery(Guid FuncaoId) : IRequest<List<CatalogoEpcDto>>;

public class ListarEpcsPorFuncaoQueryHandler : IRequestHandler<ListarEpcsPorFuncaoQuery, List<CatalogoEpcDto>>
{
    private readonly IAppDbContext _db;
    public ListarEpcsPorFuncaoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<CatalogoEpcDto>> Handle(ListarEpcsPorFuncaoQuery request, CancellationToken ct)
        => await _db.MatrizEpcFuncoes
            .Where(m => m.FuncaoId == request.FuncaoId)
            .OrderBy(m => m.CatalogoEpc!.Nome)
            .Select(m => new CatalogoEpcDto(m.CatalogoEpc!.Id, m.CatalogoEpc!.Nome, m.CatalogoEpc!.Categoria, m.CatalogoEpc!.FotoConteudo != null))
            .ToListAsync(ct);
}

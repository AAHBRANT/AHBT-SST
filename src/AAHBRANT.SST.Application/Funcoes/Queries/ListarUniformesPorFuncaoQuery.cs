using AAHBRANT.SST.Application.CatalogosUniforme;
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Funcoes.Queries;

public record ListarUniformesPorFuncaoQuery(Guid FuncaoId) : IRequest<List<CatalogoUniformeDto>>;

public class ListarUniformesPorFuncaoQueryHandler : IRequestHandler<ListarUniformesPorFuncaoQuery, List<CatalogoUniformeDto>>
{
    private readonly IAppDbContext _db;
    public ListarUniformesPorFuncaoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<CatalogoUniformeDto>> Handle(ListarUniformesPorFuncaoQuery request, CancellationToken ct)
        => await _db.MatrizUniformeFuncoes
            .Where(m => m.FuncaoId == request.FuncaoId)
            .OrderBy(m => m.CatalogoUniforme!.Nome)
            .Select(m => new CatalogoUniformeDto(m.CatalogoUniforme!.Id, m.CatalogoUniforme!.Nome, m.CatalogoUniforme!.Categoria, m.CatalogoUniforme!.FotoConteudo != null))
            .ToListAsync(ct);
}

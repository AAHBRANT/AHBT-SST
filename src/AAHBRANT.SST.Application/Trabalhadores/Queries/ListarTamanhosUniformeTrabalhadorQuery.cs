using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Trabalhadores.Queries;

public record ListarTamanhosUniformeTrabalhadorQuery(Guid TrabalhadorId) : IRequest<List<TamanhoUniformeTrabalhadorDto>>;

public class ListarTamanhosUniformeTrabalhadorQueryHandler : IRequestHandler<ListarTamanhosUniformeTrabalhadorQuery, List<TamanhoUniformeTrabalhadorDto>>
{
    private readonly IAppDbContext _db;
    public ListarTamanhosUniformeTrabalhadorQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<TamanhoUniformeTrabalhadorDto>> Handle(ListarTamanhosUniformeTrabalhadorQuery request, CancellationToken ct)
        => await _db.TrabalhadorTamanhosUniforme
            .Where(t => t.TrabalhadorId == request.TrabalhadorId)
            .OrderBy(t => t.CatalogoUniforme!.Nome)
            .Select(t => new TamanhoUniformeTrabalhadorDto(t.CatalogoUniformeId, t.CatalogoUniforme!.Nome, t.Tamanho))
            .ToListAsync(ct);
}

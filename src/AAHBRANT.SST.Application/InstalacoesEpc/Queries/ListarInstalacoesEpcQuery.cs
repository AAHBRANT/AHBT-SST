using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.InstalacoesEpc.Queries;

public record ListarInstalacoesEpcQuery(Guid? ObraId = null) : IRequest<List<InstalacaoEpcDto>>;

public class ListarInstalacoesEpcQueryHandler : IRequestHandler<ListarInstalacoesEpcQuery, List<InstalacaoEpcDto>>
{
    private readonly IAppDbContext _db;
    public ListarInstalacoesEpcQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<InstalacaoEpcDto>> Handle(ListarInstalacoesEpcQuery request, CancellationToken ct)
    {
        var query = _db.InstalacoesEpc.Where(x => x.Ativo);
        if (request.ObraId is not null)
            query = query.Where(x => x.ObraId == request.ObraId);

        return await query
            .OrderByDescending(x => x.DataInstalacao)
            .Select(x => new InstalacaoEpcDto(
                x.Id,
                x.CatalogoEpcId,
                x.ObraId,
                x.LocalInstalacao,
                x.Quantidade,
                x.DataInstalacao,
                x.DataValidade,
                x.DataUltimaInspecao,
                x.StatusUltimaInspecao,
                x.ObservacoesInspecao,
                x.DataRemocao,
                x.Observacoes))
            .ToListAsync(ct);
    }
}

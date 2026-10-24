using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.InstalacoesEpc.Queries;

public record ObterInstalacaoEpcPorIdQuery(Guid Id) : IRequest<InstalacaoEpcDto?>;

public class ObterInstalacaoEpcPorIdQueryHandler : IRequestHandler<ObterInstalacaoEpcPorIdQuery, InstalacaoEpcDto?>
{
    private readonly IAppDbContext _db;
    public ObterInstalacaoEpcPorIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<InstalacaoEpcDto?> Handle(ObterInstalacaoEpcPorIdQuery request, CancellationToken ct)
        => await _db.InstalacoesEpc
            .Where(x => x.Id == request.Id && x.Ativo)
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
            .FirstOrDefaultAsync(ct);
}

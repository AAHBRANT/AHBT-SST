using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.MatrizRisco.Queries;

public record ObterMatrizRiscoConfigPorIdQuery(Guid Id) : IRequest<MatrizRiscoConfigDto?>;

public class ObterMatrizRiscoConfigPorIdQueryHandler : IRequestHandler<ObterMatrizRiscoConfigPorIdQuery, MatrizRiscoConfigDto?>
{
    private readonly IAppDbContext _db;

    public ObterMatrizRiscoConfigPorIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<MatrizRiscoConfigDto?> Handle(ObterMatrizRiscoConfigPorIdQuery request, CancellationToken ct)
    {
        var config = await _db.MatrizRiscoConfigs
            .Include(c => c.Celulas)
            .FirstOrDefaultAsync(c => c.Id == request.Id, ct);

        return config is null ? null : MatrizRiscoMapeamento.ParaDto(config);
    }
}

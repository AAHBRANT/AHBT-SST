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

        if (config is null) return null;

        return new MatrizRiscoConfigDto
        {
            Id = config.Id,
            Nome = config.Nome,
            NumNiveisProbabilidade = config.NumNiveisProbabilidade,
            NumNiveisSeveridade = config.NumNiveisSeveridade,
            Celulas = config.Celulas.Select(cel => new MatrizRiscoCelulaDto
            {
                Probabilidade = cel.Probabilidade,
                Severidade = cel.Severidade,
                NivelRisco = cel.NivelRisco
            }).ToList()
        };
    }
}

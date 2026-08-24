using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.MatrizRisco.Queries;

public record ListarMatrizRiscoConfigsQuery : IRequest<List<MatrizRiscoConfigDto>>;

public class ListarMatrizRiscoConfigsQueryHandler : IRequestHandler<ListarMatrizRiscoConfigsQuery, List<MatrizRiscoConfigDto>>
{
    private readonly IAppDbContext _db;

    public ListarMatrizRiscoConfigsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<MatrizRiscoConfigDto>> Handle(ListarMatrizRiscoConfigsQuery request, CancellationToken ct)
    {
        var configs = await _db.MatrizRiscoConfigs
            .Include(c => c.Celulas)
            .OrderBy(c => c.Nome)
            .ToListAsync(ct);

        return configs.Select(c => new MatrizRiscoConfigDto
        {
            Id = c.Id,
            Nome = c.Nome,
            NumNiveisProbabilidade = c.NumNiveisProbabilidade,
            NumNiveisSeveridade = c.NumNiveisSeveridade,
            Celulas = c.Celulas.Select(cel => new MatrizRiscoCelulaDto
            {
                Probabilidade = cel.Probabilidade,
                Severidade = cel.Severidade,
                NivelRisco = cel.NivelRisco
            }).ToList()
        }).ToList();
    }
}

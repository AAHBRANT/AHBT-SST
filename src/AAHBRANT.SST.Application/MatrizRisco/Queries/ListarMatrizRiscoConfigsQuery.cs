using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.MatrizRisco.Queries;

// Todas as versões, da mais nova para a mais antiga: a vigente primeiro e o histórico depois.
public record ListarMatrizRiscoConfigsQuery : IRequest<List<MatrizRiscoConfigDto>>;

public class ListarMatrizRiscoConfigsQueryHandler : IRequestHandler<ListarMatrizRiscoConfigsQuery, List<MatrizRiscoConfigDto>>
{
    private readonly IAppDbContext _db;

    public ListarMatrizRiscoConfigsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<MatrizRiscoConfigDto>> Handle(ListarMatrizRiscoConfigsQuery request, CancellationToken ct)
    {
        var configs = await _db.MatrizRiscoConfigs
            .Include(c => c.Celulas)
            .OrderByDescending(c => c.Versao)
            .ToListAsync(ct);

        return configs.Select(MatrizRiscoMapeamento.ParaDto).ToList();
    }
}

// A matriz que classifica os riscos hoje; nulo enquanto a empresa não cadastrou a primeira.
public record ObterMatrizRiscoVigenteQuery : IRequest<MatrizRiscoConfigDto?>;

public class ObterMatrizRiscoVigenteQueryHandler : IRequestHandler<ObterMatrizRiscoVigenteQuery, MatrizRiscoConfigDto?>
{
    private readonly IAppDbContext _db;

    public ObterMatrizRiscoVigenteQueryHandler(IAppDbContext db) => _db = db;

    public async Task<MatrizRiscoConfigDto?> Handle(ObterMatrizRiscoVigenteQuery request, CancellationToken ct)
    {
        var config = await _db.MatrizRiscoConfigs.Include(c => c.Celulas).VigenteMaisRecente().FirstOrDefaultAsync(ct);
        return config is null ? null : MatrizRiscoMapeamento.ParaDto(config);
    }
}

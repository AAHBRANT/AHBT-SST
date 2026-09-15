using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Treinamentos.Queries;

public record ListarTreinamentosQuery(Guid? TrabalhadorId = null, Guid? ObraId = null) : IRequest<List<TreinamentoDto>>;

public class ListarTreinamentosQueryHandler : IRequestHandler<ListarTreinamentosQuery, List<TreinamentoDto>>
{
    private readonly IAppDbContext _db;
    public ListarTreinamentosQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<TreinamentoDto>> Handle(ListarTreinamentosQuery request, CancellationToken ct)
    {
        var query = _db.Treinamentos.AsNoTracking().AsQueryable();
        if (request.TrabalhadorId is not null)
            query = query.Where(x => x.TrabalhadorId == request.TrabalhadorId);

        // Filtro por obra via Trabalhador.ObraId — usado pelo Dashboard (ver ListarAsosQuery).
        if (request.ObraId is not null)
            query = query.Where(x => x.Trabalhador != null && x.Trabalhador.ObraId == request.ObraId.Value);

        return await query
            .OrderByDescending(x => x.DataValidade)
            .Select(x => new TreinamentoDto(
                x.Id,
                x.TrabalhadorId,
                x.CursoTreinamentoId,
                x.DataRealizacao,
                x.DataValidade,
                x.CargaHorariaRealizada,
                x.InstituicaoInstrutor,
                x.NumeroCertificado,
                x.Local,
                x.InstrutorRegistroProfissional))
            .ToListAsync(ct);
    }
}

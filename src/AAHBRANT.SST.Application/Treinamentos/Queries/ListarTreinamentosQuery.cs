using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Treinamentos.Queries;

public record ListarTreinamentosQuery(Guid? TrabalhadorId = null) : IRequest<List<TreinamentoDto>>;

public class ListarTreinamentosQueryHandler : IRequestHandler<ListarTreinamentosQuery, List<TreinamentoDto>>
{
    private readonly IAppDbContext _db;
    public ListarTreinamentosQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<TreinamentoDto>> Handle(ListarTreinamentosQuery request, CancellationToken ct)
    {
        var query = _db.Treinamentos.AsQueryable();
        if (request.TrabalhadorId is not null)
            query = query.Where(x => x.TrabalhadorId == request.TrabalhadorId);

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
                x.NumeroCertificado))
            .ToListAsync(ct);
    }
}

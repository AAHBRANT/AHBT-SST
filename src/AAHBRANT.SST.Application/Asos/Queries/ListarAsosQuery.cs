using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Asos.Queries;

public record ListarAsosQuery(Guid? TrabalhadorId = null) : IRequest<List<AsoDto>>;

public class ListarAsosQueryHandler : IRequestHandler<ListarAsosQuery, List<AsoDto>>
{
    private readonly IAppDbContext _db;

    public ListarAsosQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<AsoDto>> Handle(ListarAsosQuery request, CancellationToken ct)
    {
        var query = _db.Asos.AsQueryable();

        if (request.TrabalhadorId.HasValue)
            query = query.Where(a => a.TrabalhadorId == request.TrabalhadorId.Value);

        return await query
            .OrderByDescending(a => a.DataValidade)
            .Select(a => new AsoDto
            {
                Id = a.Id,
                TrabalhadorId = a.TrabalhadorId,
                Tipo = a.Tipo,
                DataExame = a.DataExame,
                DataValidade = a.DataValidade,
                ResultadoStatus = a.ResultadoStatus,
                MedicoNome = a.MedicoNome,
                MedicoCrm = a.MedicoCrm,
                ObservacoesClinicas = a.ObservacoesClinicas
            })
            .ToListAsync(ct);
    }
}

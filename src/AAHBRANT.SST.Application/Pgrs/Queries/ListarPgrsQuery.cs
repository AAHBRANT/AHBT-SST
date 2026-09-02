using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Pgrs.Queries;

public record ListarPgrsQuery(Guid? ObraId = null) : IRequest<List<PgrDto>>;

public class ListarPgrsQueryHandler : IRequestHandler<ListarPgrsQuery, List<PgrDto>>
{
    private readonly IAppDbContext _db;

    public ListarPgrsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<PgrDto>> Handle(ListarPgrsQuery request, CancellationToken ct)
    {
        var query = _db.Pgrs.AsQueryable();

        if (request.ObraId.HasValue)
            query = query.Where(p => p.ObraId == request.ObraId.Value);

        var pgrs = await query.OrderByDescending(p => p.CreatedAtUtc).ToListAsync(ct);

        return pgrs.Select(p => new PgrDto
        {
            Id = p.Id,
            ObraId = p.ObraId,
            Nome = p.Nome,
            Descricao = p.Descricao,
            DataElaboracao = p.DataElaboracao,
            DataProximaRevisao = p.DataProximaRevisao,
            DataTermino = p.DataTermino,
            ResponsavelUsuarioId = p.ResponsavelUsuarioId,
            Status = p.Status
        }).ToList();
    }
}

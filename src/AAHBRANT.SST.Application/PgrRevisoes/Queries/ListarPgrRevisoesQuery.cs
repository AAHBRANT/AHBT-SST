using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Pgrs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PgrRevisoes.Queries;

public record ListarPgrRevisoesQuery(Guid PgrId) : IRequest<List<PgrRevisaoDto>>;

public class ListarPgrRevisoesQueryHandler : IRequestHandler<ListarPgrRevisoesQuery, List<PgrRevisaoDto>>
{
    private readonly IAppDbContext _db;

    public ListarPgrRevisoesQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<PgrRevisaoDto>> Handle(ListarPgrRevisoesQuery request, CancellationToken ct)
    {
        var revisoes = await _db.PgrRevisoes
            .Where(r => r.PgrId == request.PgrId)
            .OrderByDescending(r => r.NumeroRevisao)
            .ToListAsync(ct);

        return revisoes.Select(r => new PgrRevisaoDto
        {
            Id = r.Id,
            PgrId = r.PgrId,
            NumeroRevisao = r.NumeroRevisao,
            DataRevisao = r.DataRevisao,
            Motivo = r.Motivo,
            ResponsavelUsuarioId = r.ResponsavelUsuarioId
        }).ToList();
    }
}

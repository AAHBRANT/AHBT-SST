using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Setores.Queries;

public record ListarSetoresQuery(Guid? ObraId) : IRequest<List<SetorDto>>;

public class ListarSetoresQueryHandler : IRequestHandler<ListarSetoresQuery, List<SetorDto>>
{
    private readonly IAppDbContext _db;

    public ListarSetoresQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<SetorDto>> Handle(ListarSetoresQuery request, CancellationToken ct)
    {
        var query = _db.Setores.AsQueryable();
        if (request.ObraId.HasValue)
        {
            query = query.Where(s => s.ObraId == request.ObraId.Value);
        }

        return await query
            .OrderBy(s => s.Nome)
            .Select(s => new SetorDto
            {
                Id = s.Id,
                ObraId = s.ObraId,
                ObraNome = s.Obra!.Nome,
                Nome = s.Nome
            })
            .ToListAsync(ct);
    }
}

using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.AreasSst.Queries;

public record ListarAreasSstQuery(Guid? ObraId = null) : IRequest<List<AreaSstDto>>;

public class ListarAreasSstQueryHandler : IRequestHandler<ListarAreasSstQuery, List<AreaSstDto>>
{
    private readonly IAppDbContext _db;

    public ListarAreasSstQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<AreaSstDto>> Handle(ListarAreasSstQuery request, CancellationToken ct)
    {
        var query = _db.AreasSst.AsQueryable();

        if (request.ObraId.HasValue)
            query = query.Where(a => a.ObraId == request.ObraId.Value);

        return await query
            .OrderBy(a => a.Codigo)
            .Select(a => new AreaSstDto
            {
                Id = a.Id,
                Codigo = a.Codigo,
                Nome = a.Nome,
                Tipo = a.Tipo,
                ObraId = a.ObraId,
                DetalhesLocalizacao = a.DetalhesLocalizacao,
                Riscos = a.Riscos,
                Requisitos = a.Requisitos,
                Status = a.Status
            })
            .ToListAsync(ct);
    }
}

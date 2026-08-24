using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Atividades.Queries;

public record ListarAtividadesQuery(Guid? ObraId = null) : IRequest<List<AtividadeDto>>;

public class ListarAtividadesQueryHandler : IRequestHandler<ListarAtividadesQuery, List<AtividadeDto>>
{
    private readonly IAppDbContext _db;

    public ListarAtividadesQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<AtividadeDto>> Handle(ListarAtividadesQuery request, CancellationToken ct)
    {
        var query = _db.Atividades.AsQueryable();

        if (request.ObraId.HasValue)
            query = query.Where(a => a.ObraId == request.ObraId.Value);

        return await query
            .OrderBy(a => a.Nome)
            .Select(a => new AtividadeDto
            {
                Id = a.Id,
                ObraId = a.ObraId,
                Nome = a.Nome,
                Descricao = a.Descricao
            })
            .ToListAsync(ct);
    }
}

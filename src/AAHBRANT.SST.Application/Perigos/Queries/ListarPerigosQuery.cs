using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Perigos.Queries;

public record ListarPerigosQuery : IRequest<List<PerigoDto>>;

public class ListarPerigosQueryHandler : IRequestHandler<ListarPerigosQuery, List<PerigoDto>>
{
    private readonly IAppDbContext _db;

    public ListarPerigosQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<PerigoDto>> Handle(ListarPerigosQuery request, CancellationToken ct)
    {
        return await _db.Perigos
            .OrderBy(p => p.Nome)
            .Select(p => new PerigoDto
            {
                Id = p.Id,
                Nome = p.Nome,
                Agente = p.Agente,
                Fonte = p.Fonte,
                Descricao = p.Descricao
            })
            .ToListAsync(ct);
    }
}

using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Perigos.Queries;

public record ObterPerigoPorIdQuery(Guid Id) : IRequest<PerigoDto?>;

public class ObterPerigoPorIdQueryHandler : IRequestHandler<ObterPerigoPorIdQuery, PerigoDto?>
{
    private readonly IAppDbContext _db;

    public ObterPerigoPorIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<PerigoDto?> Handle(ObterPerigoPorIdQuery request, CancellationToken ct)
    {
        return await _db.Perigos
            .Where(p => p.Id == request.Id)
            .Select(p => new PerigoDto
            {
                Id = p.Id,
                Nome = p.Nome,
                Agente = p.Agente,
                Fonte = p.Fonte,
                Descricao = p.Descricao
            })
            .FirstOrDefaultAsync(ct);
    }
}

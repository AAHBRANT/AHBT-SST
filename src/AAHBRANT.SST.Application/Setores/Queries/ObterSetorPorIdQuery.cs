using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Setores.Queries;

public record ObterSetorPorIdQuery(Guid Id) : IRequest<SetorDto?>;

public class ObterSetorPorIdQueryHandler : IRequestHandler<ObterSetorPorIdQuery, SetorDto?>
{
    private readonly IAppDbContext _db;

    public ObterSetorPorIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<SetorDto?> Handle(ObterSetorPorIdQuery request, CancellationToken ct)
    {
        return await _db.Setores
            .Where(s => s.Id == request.Id)
            .Select(s => new SetorDto
            {
                Id = s.Id,
                ObraId = s.ObraId,
                ObraNome = s.Obra!.Nome,
                Nome = s.Nome
            })
            .FirstOrDefaultAsync(ct);
    }
}

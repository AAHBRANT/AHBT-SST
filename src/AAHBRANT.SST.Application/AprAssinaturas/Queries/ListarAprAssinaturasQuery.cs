using AAHBRANT.SST.Application.Aprs;
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.AprAssinaturas.Queries;

public record ListarAprAssinaturasQuery(Guid AprId) : IRequest<List<AprAssinaturaDto>>;

public class ListarAprAssinaturasQueryHandler : IRequestHandler<ListarAprAssinaturasQuery, List<AprAssinaturaDto>>
{
    private readonly IAppDbContext _db;

    public ListarAprAssinaturasQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<AprAssinaturaDto>> Handle(ListarAprAssinaturasQuery request, CancellationToken ct)
    {
        var assinaturas = await _db.AprAssinaturas
            .Where(s => s.AprId == request.AprId)
            .Include(s => s.Trabalhador)
            .OrderByDescending(s => s.DataAssinatura)
            .ToListAsync(ct);

        return assinaturas.Select(s => new AprAssinaturaDto
        {
            Id = s.Id,
            AprId = s.AprId,
            TrabalhadorId = s.TrabalhadorId,
            TrabalhadorNome = s.Trabalhador?.Nome ?? string.Empty,
            Papel = s.Papel,
            DataAssinatura = s.DataAssinatura
        }).ToList();
    }
}

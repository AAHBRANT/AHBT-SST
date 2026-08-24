using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Riscos.Queries;

public record ListarRiscosQuery(Guid? AtividadeId = null) : IRequest<List<RiscoDto>>;

public class ListarRiscosQueryHandler : IRequestHandler<ListarRiscosQuery, List<RiscoDto>>
{
    private readonly IAppDbContext _db;

    public ListarRiscosQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<RiscoDto>> Handle(ListarRiscosQuery request, CancellationToken ct)
    {
        var query = _db.Riscos.Include(r => r.TrabalhadoresExpostos).AsQueryable();

        if (request.AtividadeId.HasValue)
            query = query.Where(r => r.AtividadeId == request.AtividadeId.Value);

        var riscos = await query.OrderByDescending(r => r.CreatedAtUtc).ToListAsync(ct);

        return riscos.Select(r => new RiscoDto
        {
            Id = r.Id,
            AtividadeId = r.AtividadeId,
            PerigoId = r.PerigoId,
            Ambiente = r.Ambiente,
            Exposicao = r.Exposicao,
            Consequencia = r.Consequencia,
            Probabilidade = r.Probabilidade,
            Severidade = r.Severidade,
            NivelRisco = r.NivelRisco,
            ControlesExistentes = r.ControlesExistentes,
            ControlesAdicionais = r.ControlesAdicionais,
            ResponsavelUsuarioId = r.ResponsavelUsuarioId,
            Prazo = r.Prazo,
            Status = r.Status,
            TrabalhadoresExpostosIds = r.TrabalhadoresExpostos.Select(t => t.TrabalhadorId).ToList()
        }).ToList();
    }
}

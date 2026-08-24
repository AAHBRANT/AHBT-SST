using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Riscos.Queries;

public record ObterRiscoPorIdQuery(Guid Id) : IRequest<RiscoDto?>;

public class ObterRiscoPorIdQueryHandler : IRequestHandler<ObterRiscoPorIdQuery, RiscoDto?>
{
    private readonly IAppDbContext _db;

    public ObterRiscoPorIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<RiscoDto?> Handle(ObterRiscoPorIdQuery request, CancellationToken ct)
    {
        var r = await _db.Riscos
            .Include(x => x.TrabalhadoresExpostos)
            .FirstOrDefaultAsync(x => x.Id == request.Id, ct);

        if (r is null) return null;

        return new RiscoDto
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
        };
    }
}

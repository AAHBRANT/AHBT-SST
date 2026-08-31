using AAHBRANT.SST.Application.Aprs;
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.AprEtapas.Queries;

public record ListarAprEtapasQuery(Guid AprId) : IRequest<List<AprEtapaDto>>;

public class ListarAprEtapasQueryHandler : IRequestHandler<ListarAprEtapasQuery, List<AprEtapaDto>>
{
    private readonly IAppDbContext _db;

    public ListarAprEtapasQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<AprEtapaDto>> Handle(ListarAprEtapasQuery request, CancellationToken ct)
    {
        var etapas = await _db.AprEtapas
            .Where(e => e.AprId == request.AprId)
            .Include(e => e.Riscos)
            .OrderBy(e => e.Ordem)
            .ToListAsync(ct);

        return etapas.Select(e => new AprEtapaDto
        {
            Id = e.Id,
            AprId = e.AprId,
            Ordem = e.Ordem,
            Descricao = e.Descricao,
            Riscos = e.Riscos.Select(r => new AprEtapaRiscoDto
            {
                Id = r.Id,
                AprEtapaId = r.AprEtapaId,
                PerigoEventoPerigoso = r.PerigoEventoPerigoso,
                FonteCircunstancia = r.FonteCircunstancia,
                PossiveisLesoes = r.PossiveisLesoes,
                TrabalhadoresExpostos = r.TrabalhadoresExpostos,
                ProbabilidadeInicial = r.ProbabilidadeInicial,
                SeveridadeInicial = r.SeveridadeInicial,
                NivelRiscoInicial = r.NivelRiscoInicial,
                MedidasPrevencao = r.MedidasPrevencao,
                Responsavel = r.Responsavel,
                ProbabilidadeResidual = r.ProbabilidadeResidual,
                SeveridadeResidual = r.SeveridadeResidual,
                NivelRiscoResidual = r.NivelRiscoResidual,
            }).ToList()
        }).ToList();
    }
}

using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cipa.Queries;

public record ListarInspecoesCipaQuery(Guid? ObraId = null) : IRequest<List<InspecaoCipaDto>>;

public class ListarInspecoesCipaQueryHandler : IRequestHandler<ListarInspecoesCipaQuery, List<InspecaoCipaDto>>
{
    private readonly IAppDbContext _db;
    public ListarInspecoesCipaQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<InspecaoCipaDto>> Handle(ListarInspecoesCipaQuery request, CancellationToken ct)
    {
        var query = _db.InspecoesCipa
            .Include(i => i.Obra)
            .Include(i => i.MembroCipa).ThenInclude(m => m!.Trabalhador)
            .AsQueryable();
        if (request.ObraId.HasValue) query = query.Where(i => i.ObraId == request.ObraId.Value);

        return await query
            .OrderByDescending(i => i.Data)
            .Select(i => new InspecaoCipaDto
            {
                Id = i.Id,
                ObraId = i.ObraId,
                ObraNome = i.Obra!.Nome,
                MembroCipaId = i.MembroCipaId,
                MembroCipaNome = i.MembroCipa != null ? i.MembroCipa.Trabalhador!.Nome : null,
                Data = i.Data,
                Local = i.Local,
                RiscoIdentificado = i.RiscoIdentificado,
                GrauRisco = i.GrauRisco,
                NaoConformidadeId = i.NaoConformidadeId,
            })
            .ToListAsync(ct);
    }
}

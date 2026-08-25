using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Pcmso.Queries;

public record ListarPcmsosQuery(Guid? ObraId = null) : IRequest<List<PcmsoDto>>;

public class ListarPcmsosQueryHandler : IRequestHandler<ListarPcmsosQuery, List<PcmsoDto>>
{
    private readonly IAppDbContext _db;

    public ListarPcmsosQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<PcmsoDto>> Handle(ListarPcmsosQuery request, CancellationToken ct)
    {
        var query = _db.Pcmsos.AsQueryable();

        if (request.ObraId.HasValue)
            query = query.Where(p => p.ObraId == request.ObraId.Value);

        var pcmsos = await query.OrderByDescending(p => p.CreatedAtUtc).ToListAsync(ct);

        return pcmsos.Select(p => new PcmsoDto
        {
            Id = p.Id,
            ObraId = p.ObraId,
            Nome = p.Nome,
            Objetivo = p.Objetivo,
            MedicoCoordenadorNome = p.MedicoCoordenadorNome,
            MedicoCoordenadorCrm = p.MedicoCoordenadorCrm,
            MedicoCoordenadorUsuarioId = p.MedicoCoordenadorUsuarioId,
            DataElaboracao = p.DataElaboracao,
            DataVigenciaInicio = p.DataVigenciaInicio,
            DataVigenciaFim = p.DataVigenciaFim,
            Status = p.Status,
        }).ToList();
    }
}

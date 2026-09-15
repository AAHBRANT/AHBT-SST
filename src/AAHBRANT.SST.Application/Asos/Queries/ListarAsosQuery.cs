using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Asos.Queries;

public record ListarAsosQuery(Guid? TrabalhadorId = null, Guid? ObraId = null) : IRequest<List<AsoDto>>;

public class ListarAsosQueryHandler : IRequestHandler<ListarAsosQuery, List<AsoDto>>
{
    private readonly IAppDbContext _db;

    public ListarAsosQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<AsoDto>> Handle(ListarAsosQuery request, CancellationToken ct)
    {
        var query = _db.Asos.AsNoTracking().AsQueryable();

        if (request.TrabalhadorId.HasValue)
            query = query.Where(a => a.TrabalhadorId == request.TrabalhadorId.Value);

        // Filtro por obra via Trabalhador.ObraId — usado pelo Dashboard (que hoje filtra a empresa
        // inteira e recorta no cliente); permite trazer só a obra selecionada direto do servidor.
        if (request.ObraId.HasValue)
            query = query.Where(a => a.Trabalhador != null && a.Trabalhador.ObraId == request.ObraId.Value);

        return await query
            .OrderByDescending(a => a.DataValidade)
            .Select(a => new AsoDto
            {
                Id = a.Id,
                TrabalhadorId = a.TrabalhadorId,
                Tipo = a.Tipo,
                DataExame = a.DataExame,
                DataValidade = a.DataValidade,
                ResultadoStatus = a.ResultadoStatus,
                MedicoNome = a.MedicoNome,
                MedicoCrm = a.MedicoCrm,
                ObservacoesClinicas = a.ObservacoesClinicas
            })
            .ToListAsync(ct);
    }
}

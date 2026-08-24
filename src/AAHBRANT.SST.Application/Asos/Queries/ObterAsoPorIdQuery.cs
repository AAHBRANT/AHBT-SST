using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Asos.Queries;

public record ObterAsoPorIdQuery(Guid Id) : IRequest<AsoDto?>;

public class ObterAsoPorIdQueryHandler : IRequestHandler<ObterAsoPorIdQuery, AsoDto?>
{
    private readonly IAppDbContext _db;

    public ObterAsoPorIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<AsoDto?> Handle(ObterAsoPorIdQuery request, CancellationToken ct)
    {
        return await _db.Asos
            .Where(a => a.Id == request.Id)
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
            .FirstOrDefaultAsync(ct);
    }
}

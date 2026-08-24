using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CursosTreinamento.Queries;

public record ObterCursoTreinamentoPorIdQuery(Guid Id) : IRequest<CursoTreinamentoDto?>;

public class ObterCursoTreinamentoPorIdQueryHandler : IRequestHandler<ObterCursoTreinamentoPorIdQuery, CursoTreinamentoDto?>
{
    private readonly IAppDbContext _db;
    public ObterCursoTreinamentoPorIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<CursoTreinamentoDto?> Handle(ObterCursoTreinamentoPorIdQuery request, CancellationToken ct)
        => await _db.CursosTreinamento
            .Where(x => x.Id == request.Id)
            .Select(x => new CursoTreinamentoDto(x.Id, x.Nome, x.NormaReferencia, x.CargaHorariaMinima, x.ValidadeEmMeses))
            .FirstOrDefaultAsync(ct);
}

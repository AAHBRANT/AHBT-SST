using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CursosTreinamento.Queries;

public record ListarCursosTreinamentoQuery : IRequest<List<CursoTreinamentoDto>>;

public class ListarCursosTreinamentoQueryHandler : IRequestHandler<ListarCursosTreinamentoQuery, List<CursoTreinamentoDto>>
{
    private readonly IAppDbContext _db;
    public ListarCursosTreinamentoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<CursoTreinamentoDto>> Handle(ListarCursosTreinamentoQuery request, CancellationToken ct)
        => await _db.CursosTreinamento
            .OrderBy(x => x.Nome)
            .Select(x => new CursoTreinamentoDto(x.Id, x.Nome, x.NormaReferencia, x.CargaHorariaMinima, x.ValidadeEmMeses))
            .ToListAsync(ct);
}

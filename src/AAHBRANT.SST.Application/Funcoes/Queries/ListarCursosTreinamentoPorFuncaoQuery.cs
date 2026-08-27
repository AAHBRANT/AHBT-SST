using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.CursosTreinamento;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Funcoes.Queries;

public record ListarCursosTreinamentoPorFuncaoQuery(Guid FuncaoId) : IRequest<List<CursoTreinamentoDto>>;

public class ListarCursosTreinamentoPorFuncaoQueryHandler
    : IRequestHandler<ListarCursosTreinamentoPorFuncaoQuery, List<CursoTreinamentoDto>>
{
    private readonly IAppDbContext _db;
    public ListarCursosTreinamentoPorFuncaoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<CursoTreinamentoDto>> Handle(ListarCursosTreinamentoPorFuncaoQuery request, CancellationToken ct)
        => await _db.MatrizTreinamentoFuncoes
            .Where(m => m.FuncaoId == request.FuncaoId)
            .Select(m => m.CursoTreinamento!)
            .Select(c => new CursoTreinamentoDto(
                c.Id,
                c.Nome,
                c.NormaReferencia,
                c.CargaHorariaMinima,
                c.ValidadeEmMeses,
                c.ConteudoProgramatico))
            .ToListAsync(ct);
}

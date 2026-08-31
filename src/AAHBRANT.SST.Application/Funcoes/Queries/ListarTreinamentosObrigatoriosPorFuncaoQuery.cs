using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.CursosTreinamento;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Funcoes.Queries;

public record ListarTreinamentosObrigatoriosPorFuncaoQuery(Guid FuncaoId) : IRequest<List<CursoTreinamentoDto>>;

public class ListarTreinamentosObrigatoriosPorFuncaoQueryHandler
    : IRequestHandler<ListarTreinamentosObrigatoriosPorFuncaoQuery, List<CursoTreinamentoDto>>
{
    private readonly IAppDbContext _db;
    public ListarTreinamentosObrigatoriosPorFuncaoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<CursoTreinamentoDto>> Handle(ListarTreinamentosObrigatoriosPorFuncaoQuery request, CancellationToken ct)
        => await _db.MatrizTreinamentoFuncoes
            .Where(m => m.FuncaoId == request.FuncaoId)
            .OrderBy(m => m.CursoTreinamento!.Nome)
            .Select(m => new CursoTreinamentoDto(
                m.CursoTreinamento!.Id,
                m.CursoTreinamento!.Nome,
                m.CursoTreinamento!.NormaReferencia,
                m.CursoTreinamento!.CargaHorariaMinima,
                m.CursoTreinamento!.ValidadeEmMeses))
            .ToListAsync(ct);
}

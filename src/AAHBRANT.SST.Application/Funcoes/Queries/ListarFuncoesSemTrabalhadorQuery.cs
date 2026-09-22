using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Funcoes.Queries;

public record FuncaoSemTrabalhadorDto(
    Guid Id,
    string Nome,
    string? CboCodigo,
    bool TemEpiNaMatriz,
    bool TemTreinamentoNaMatriz,
    bool TemUniformeNaMatriz);

public record ListarFuncoesSemTrabalhadorQuery : IRequest<List<FuncaoSemTrabalhadorDto>>;

// Pedido do usuário (22/09): visão ampla, só pra revisão manual — ao contrário de
// ListarFuncoesOrfasQuery (que só devolve o subconjunto seguro pra exclusão automática, sem CBO e
// sem nenhuma matriz), aqui aparece QUALQUER função sem trabalhador vinculado, mesmo com CBO ou com
// matriz de EPI/treinamento/uniforme — o usuário decide caso a caso, usando o botão "Excluir" já
// existente na tabela.
public class ListarFuncoesSemTrabalhadorQueryHandler
    : IRequestHandler<ListarFuncoesSemTrabalhadorQuery, List<FuncaoSemTrabalhadorDto>>
{
    private readonly IAppDbContext _db;

    public ListarFuncoesSemTrabalhadorQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<FuncaoSemTrabalhadorDto>> Handle(
        ListarFuncoesSemTrabalhadorQuery request, CancellationToken ct)
    {
        var idsComTrabalhador = await _db.Trabalhadores.IgnoreQueryFilters()
            .Where(t => t.Ativo)
            .Select(t => t.FuncaoId)
            .Distinct()
            .ToListAsync(ct);

        var idsComEpi = await _db.MatrizEpiFuncoes.Select(m => m.FuncaoId).Distinct().ToListAsync(ct);
        var idsComTreinamento = await _db.MatrizTreinamentoFuncoes.Select(m => m.FuncaoId).Distinct().ToListAsync(ct);
        var idsComUniforme = await _db.MatrizUniformeFuncoes.Select(m => m.FuncaoId).Distinct().ToListAsync(ct);

        var funcoesSemTrabalhador = await _db.Funcoes
            .Where(f => !idsComTrabalhador.Contains(f.Id))
            .Select(f => new { f.Id, f.Nome, f.CboCodigo })
            .ToListAsync(ct);

        return funcoesSemTrabalhador
            .Select(f => new FuncaoSemTrabalhadorDto(
                f.Id,
                f.Nome,
                f.CboCodigo,
                idsComEpi.Contains(f.Id),
                idsComTreinamento.Contains(f.Id),
                idsComUniforme.Contains(f.Id)))
            .OrderBy(f => f.Nome)
            .ToList();
    }
}

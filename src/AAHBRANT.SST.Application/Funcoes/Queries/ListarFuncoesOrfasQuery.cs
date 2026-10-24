using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Funcoes.Queries;

public record FuncaoOrfaDto(Guid Id, string Nome);

public record ListarFuncoesOrfasQuery : IRequest<List<FuncaoOrfaDto>>;

// Pedido do usuário (22/09): limpeza em massa de funções "lixo" — sem CBO (nunca sincronizadas com
// o G-RH ou criadas por engano) e sem NENHUM uso real no sistema. "Uso real" é definido em 5 sinais
// (qualquer um presente já protege a função de entrar nesta lista): CBO preenchido, matriz de EPI,
// matriz de Treinamento obrigatório, matriz de Uniforme, ou trabalhador vinculado. Função com
// qualquer um desses sinais é real e configurada de propósito — nunca aparece aqui, mesmo sem CBO.
public static class DeteccaoFuncaoOrfa
{
    // Ignora filtro de RBAC/obra do Trabalhador (IgnoreQueryFilters) — sem isso, um admin com acesso
    // restrito a algumas obras enxergaria "zero trabalhadores" numa função usada só em obra fora do
    // escopo dele, e a função seria marcada errado como órfã. Mantém o filtro de Ativo manualmente
    // (só trabalhador desligado/já excluído não conta como "em uso").
    public static async Task<HashSet<Guid>> IdsProtegidosAsync(IAppDbContext db, CancellationToken ct)
    {
        var idsComEpi = await db.MatrizEpiFuncoes.Select(m => m.FuncaoId).Distinct().ToListAsync(ct);
        var idsComTreinamento = await db.MatrizTreinamentoFuncoes.Select(m => m.FuncaoId).Distinct().ToListAsync(ct);
        var idsComUniforme = await db.MatrizUniformeFuncoes.Select(m => m.FuncaoId).Distinct().ToListAsync(ct);
        var idsComTrabalhador = await db.Trabalhadores.IgnoreQueryFilters()
            .Where(t => t.Ativo)
            .Select(t => t.FuncaoId)
            .Distinct()
            .ToListAsync(ct);

        return idsComEpi.Concat(idsComTreinamento).Concat(idsComUniforme).Concat(idsComTrabalhador).ToHashSet();
    }
}

public class ListarFuncoesOrfasQueryHandler : IRequestHandler<ListarFuncoesOrfasQuery, List<FuncaoOrfaDto>>
{
    private readonly IAppDbContext _db;

    public ListarFuncoesOrfasQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<FuncaoOrfaDto>> Handle(ListarFuncoesOrfasQuery request, CancellationToken ct)
    {
        var candidatas = await _db.Funcoes
            .Where(f => f.CboCodigo == null || f.CboCodigo == "")
            .Select(f => new { f.Id, f.Nome })
            .ToListAsync(ct);
        if (candidatas.Count == 0) return new List<FuncaoOrfaDto>();

        var idsProtegidos = await DeteccaoFuncaoOrfa.IdsProtegidosAsync(_db, ct);

        return candidatas
            .Where(f => !idsProtegidos.Contains(f.Id))
            .OrderBy(f => f.Nome)
            .Select(f => new FuncaoOrfaDto(f.Id, f.Nome))
            .ToList();
    }
}

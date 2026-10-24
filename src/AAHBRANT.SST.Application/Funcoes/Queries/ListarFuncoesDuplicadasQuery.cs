using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Funcoes.Queries;

public record FuncaoDuplicadaDto(Guid Id, string Nome, string? CboCodigo, int QuantidadeTrabalhadores);

// Só entram grupos "seguros de resolver sozinho": mesmo nome e exatamente uma das duplicatas com
// CBO preenchido — essa vira "Manter", as demais (sem CBO) viram "Remover". Grupo com 0 ou 2+ linhas
// com CBO fica de fora da lista (ambíguo demais pra decidir automaticamente) e precisa revisão manual
// direto na tela de Funções.
public record GrupoFuncaoDuplicadaDto(string Nome, FuncaoDuplicadaDto Manter, List<FuncaoDuplicadaDto> Remover);

public record ListarFuncoesDuplicadasQuery : IRequest<List<GrupoFuncaoDuplicadaDto>>;

public class ListarFuncoesDuplicadasQueryHandler : IRequestHandler<ListarFuncoesDuplicadasQuery, List<GrupoFuncaoDuplicadaDto>>
{
    private readonly IAppDbContext _db;

    public ListarFuncoesDuplicadasQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<GrupoFuncaoDuplicadaDto>> Handle(ListarFuncoesDuplicadasQuery request, CancellationToken ct)
    {
        var funcoes = await _db.Funcoes
            .Select(f => new { f.Id, f.Nome, f.CboCodigo })
            .ToListAsync(ct);

        // Contagem de trabalhadores por função ignorando o filtro de RBAC/obra (IgnoreQueryFilters)
        // e o de Ativo — o admin precisa ver o total real que será migrado, não só o que o escopo
        // dele enxerga, senão a mesclagem pode "esquecer" trabalhador de obra fora do seu acesso.
        var contagemPorFuncao = await _db.Trabalhadores.IgnoreQueryFilters()
            .GroupBy(t => t.FuncaoId)
            .Select(g => new { FuncaoId = g.Key, Quantidade = g.Count() })
            .ToDictionaryAsync(g => g.FuncaoId, g => g.Quantidade, ct);

        var resultado = new List<GrupoFuncaoDuplicadaDto>();
        foreach (var grupo in funcoes.GroupBy(f => f.Nome))
        {
            if (grupo.Count() < 2) continue;

            var comCbo = grupo.Where(f => !string.IsNullOrWhiteSpace(f.CboCodigo)).ToList();
            if (comCbo.Count != 1) continue;

            var manter = comCbo[0];
            var remover = grupo.Where(f => f.Id != manter.Id).ToList();

            resultado.Add(new GrupoFuncaoDuplicadaDto(
                grupo.Key,
                new FuncaoDuplicadaDto(manter.Id, manter.Nome, manter.CboCodigo, contagemPorFuncao.GetValueOrDefault(manter.Id)),
                remover
                    .Select(f => new FuncaoDuplicadaDto(f.Id, f.Nome, f.CboCodigo, contagemPorFuncao.GetValueOrDefault(f.Id)))
                    .ToList()));
        }
        return resultado;
    }
}

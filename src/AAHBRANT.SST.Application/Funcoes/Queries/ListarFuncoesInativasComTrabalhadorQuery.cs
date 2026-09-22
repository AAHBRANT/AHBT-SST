using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Funcoes.Queries;

public record FuncaoInativaComTrabalhadorDto(Guid Id, string Nome, int QuantidadeTrabalhadores);

public record ListarFuncoesInativasComTrabalhadorQuery : IRequest<List<FuncaoInativaComTrabalhadorDto>>;

// Diagnóstico de emergência (22/09): o botão "Excluir" de cada linha da tabela de Funções nunca teve
// as proteções de ExcluirFuncoesOrfasCommand (CBO/EPI/Treinamento/Uniforme/Trabalhador) — sempre foi
// possível excluir (soft-delete, Ativo=false) uma função que ainda tem trabalhador vinculado. Isso
// detecta esse estado quebrado: trabalhador ativo cuja função foi desativada, ficando "invisível" em
// qualquer tela normal (ficha de EPI, dashboard, etc. deixam de achar o nome/matriz da função dele).
public class ListarFuncoesInativasComTrabalhadorQueryHandler
    : IRequestHandler<ListarFuncoesInativasComTrabalhadorQuery, List<FuncaoInativaComTrabalhadorDto>>
{
    private readonly IAppDbContext _db;

    public ListarFuncoesInativasComTrabalhadorQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<FuncaoInativaComTrabalhadorDto>> Handle(
        ListarFuncoesInativasComTrabalhadorQuery request, CancellationToken ct)
    {
        var contagemPorFuncao = await _db.Trabalhadores.IgnoreQueryFilters()
            .Where(t => t.Ativo)
            .GroupBy(t => t.FuncaoId)
            .Select(g => new { FuncaoId = g.Key, Quantidade = g.Count() })
            .ToListAsync(ct);
        if (contagemPorFuncao.Count == 0) return new List<FuncaoInativaComTrabalhadorDto>();

        var idsComTrabalhador = contagemPorFuncao.Select(c => c.FuncaoId).ToHashSet();

        var funcoesInativas = await _db.Funcoes.IgnoreQueryFilters()
            .Where(f => !f.Ativo && idsComTrabalhador.Contains(f.Id))
            .Select(f => new { f.Id, f.Nome })
            .ToListAsync(ct);

        return funcoesInativas
            .Select(f => new FuncaoInativaComTrabalhadorDto(
                f.Id, f.Nome, contagemPorFuncao.First(c => c.FuncaoId == f.Id).Quantidade))
            .OrderByDescending(f => f.QuantidadeTrabalhadores)
            .ToList();
    }
}

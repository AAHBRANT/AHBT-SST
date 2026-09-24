using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Equipes.Queries;

public record ListarEquipesQuery(Guid? ObraId, Guid? SetorId) : IRequest<List<EquipeDto>>;

public class ListarEquipesQueryHandler : IRequestHandler<ListarEquipesQuery, List<EquipeDto>>
{
    private readonly IAppDbContext _db;

    public ListarEquipesQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<EquipeDto>> Handle(ListarEquipesQuery request, CancellationToken ct)
    {
        var query = _db.Equipes.AsQueryable();
        if (request.SetorId.HasValue)
        {
            query = query.Where(e => e.SetorId == request.SetorId.Value);
        }
        else if (request.ObraId.HasValue)
        {
            query = query.Where(e => e.Setor!.ObraId == request.ObraId.Value);
        }

        // Setor e obra viram consulta separada em vez de `e.Setor!.Nome` / `e.Setor.Obra!.Nome`:
        // navegação obrigatória na projeção gera INNER JOIN, e as duas entidades têm filtro global
        // por Ativo. Como "excluir" aqui é soft delete, bastava excluir um setor para a equipe
        // inteira sumir desta lista — com os trabalhadores ainda vinculados a ela no banco e sem
        // tela para reatribuí-los. Mesma armadilha da entrega de EPI em 23/09.
        var equipes = await query
            .Select(e => new
            {
                e.Id,
                e.SetorId,
                e.Nome,
                e.EncarregadoId,
                EncarregadoNome = e.Encarregado != null ? e.Encarregado.Nome : null,
                QuantidadeTrabalhadores = e.Trabalhadores.Count(t => t.Ativo),
            })
            .ToListAsync(ct);

        var setorIds = equipes.Select(e => e.SetorId).Distinct().ToList();
        var setores = await _db.Setores.AsNoTracking().IgnoreQueryFilters()
            .Where(s => setorIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Nome, s.ObraId, s.Ativo })
            .ToListAsync(ct);
        var setorPorId = setores.ToDictionary(s => s.Id);

        var obraIds = setores.Select(s => s.ObraId).Distinct().ToList();
        var nomeObraPorId = (await _db.Obras.AsNoTracking().IgnoreQueryFilters()
                .Where(o => obraIds.Contains(o.Id))
                .Select(o => new { o.Id, o.Nome, o.Ativo })
                .ToListAsync(ct))
            .ToDictionary(o => o.Id, o => o.Ativo ? o.Nome : $"{o.Nome} (obra removida)");

        return equipes
            .Select(e =>
            {
                setorPorId.TryGetValue(e.SetorId, out var setor);
                return new EquipeDto
                {
                    Id = e.Id,
                    SetorId = e.SetorId,
                    // Pai excluído aparece com o nome real e a marca — some da tela seria pior:
                    // a equipe continua com trabalhadores vinculados e precisa poder ser aberta.
                    SetorNome = setor is null ? "Setor removido" : setor.Ativo ? setor.Nome : $"{setor.Nome} (setor removido)",
                    ObraId = setor?.ObraId ?? Guid.Empty,
                    ObraNome = setor is null ? "Obra removida" : nomeObraPorId.GetValueOrDefault(setor.ObraId, "Obra removida"),
                    Nome = e.Nome,
                    EncarregadoId = e.EncarregadoId,
                    EncarregadoNome = e.EncarregadoNome,
                    QuantidadeTrabalhadores = e.QuantidadeTrabalhadores,
                };
            })
            .OrderBy(e => e.Nome)
            .ToList();
    }
}

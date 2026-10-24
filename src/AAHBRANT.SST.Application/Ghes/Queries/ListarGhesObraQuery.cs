using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Ghes.Queries;

public record GheFuncaoDto(Guid FuncaoId, string? FuncaoNome, string? Cbo, int? QuantidadeExpostos, string? DescricaoAtividades);

public record GheRiscoDto(
    Guid Id, Guid AtividadeId, string? Perigo, string? TipoAgente, string? Consequencia, string? Exposicao,
    int Probabilidade, int Severidade, NivelRisco NivelRisco, string? ControlesExistentes, string? ControlesAdicionais,
    StatusControleRisco Status);

public record GheDto(
    Guid Id, int Numero, string? Setor, string? JornadaTrabalho, string? DescricaoAmbiente, string? AtividadesCriticas,
    string? FonteGeradora, string? MedidasProtecaoExistentes, List<GheFuncaoDto> Funcoes, List<GheRiscoDto> Riscos);

public record ListarGhesObraQuery(Guid ObraId) : IRequest<List<GheDto>>;

public class ListarGhesObraQueryHandler : IRequestHandler<ListarGhesObraQuery, List<GheDto>>
{
    private readonly IAppDbContext _db;

    public ListarGhesObraQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<GheDto>> Handle(ListarGhesObraQuery request, CancellationToken ct)
    {
        // Sem navegação para Funcao/Perigo (função ou perigo excluído viraria INNER JOIN e apagaria a
        // linha — incidente de 23/09) e sem IgnoreQueryFilters na mesma consulta (desligaria os filtros de
        // Ativo/obra dos GHE): os nomes vêm de NomesPorId, em consulta separada.
        var ghes = await _db.Ghes.AsNoTracking()
            .Where(g => g.ObraId == request.ObraId)
            .OrderBy(g => g.Numero)
            .Select(g => new
            {
                g.Id, g.Numero, g.Setor, g.JornadaTrabalho, g.DescricaoAmbiente, g.AtividadesCriticas,
                g.FonteGeradora, g.MedidasProtecaoExistentes,
                Funcoes = g.Funcoes.Select(f => new { f.FuncaoId, f.QuantidadeExpostos, f.DescricaoAtividades }).ToList(),
            })
            .ToListAsync(ct);

        var gheIds = ghes.Select(g => g.Id).ToList();
        var riscos = await _db.Riscos.AsNoTracking()
            .Join(_db.Atividades.Where(a => a.GheId != null && gheIds.Contains(a.GheId.Value)),
                r => r.AtividadeId, a => a.Id, (r, a) => new { r, a.GheId })
            .ToListAsync(ct);

        var funcoes = await NomesPorId.FuncoesAsync(_db, ghes.SelectMany(g => g.Funcoes.Select(f => f.FuncaoId)), ct);
        var perigos = await NomesPorId.PerigosAsync(_db, riscos.Select(x => x.r.PerigoId), ct);

        var porGhe = riscos.ToLookup(x => x.GheId!.Value, x =>
        {
            var temPerigo = perigos.TryGetValue(x.r.PerigoId, out var perigo);
            return new GheRiscoDto(
                x.r.Id, x.r.AtividadeId, temPerigo ? perigo.Nome : null, temPerigo ? perigo.Agente : null,
                x.r.Consequencia, x.r.Exposicao, x.r.Probabilidade, x.r.Severidade, x.r.NivelRisco,
                x.r.ControlesExistentes, x.r.ControlesAdicionais, x.r.Status);
        });

        return ghes.Select(g => new GheDto(
                g.Id, g.Numero, g.Setor, g.JornadaTrabalho, g.DescricaoAmbiente, g.AtividadesCriticas,
                g.FonteGeradora, g.MedidasProtecaoExistentes,
                g.Funcoes.Select(f =>
                    {
                        var tem = funcoes.TryGetValue(f.FuncaoId, out var funcao);
                        return new GheFuncaoDto(f.FuncaoId, tem ? funcao.Nome : null, tem ? funcao.Cbo : null, f.QuantidadeExpostos, f.DescricaoAtividades);
                    })
                    .OrderBy(f => f.FuncaoNome).ToList(),
                porGhe[g.Id].OrderByDescending(r => r.NivelRisco).ThenBy(r => r.Perigo).ToList()))
            .ToList();
    }
}

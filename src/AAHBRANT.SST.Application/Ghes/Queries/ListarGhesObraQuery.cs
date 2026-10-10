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
        // Nome da função/perigo por subquery com IgnoreQueryFilters, não pela navegação: função ou perigo
        // excluído (soft delete) viraria INNER JOIN e apagaria a linha do GHE (incidente de 23/09).
        var ghes = await _db.Ghes.AsNoTracking()
            .Where(g => g.ObraId == request.ObraId)
            .OrderBy(g => g.Numero)
            .Select(g => new
            {
                g.Id, g.Numero, g.Setor, g.JornadaTrabalho, g.DescricaoAmbiente, g.AtividadesCriticas,
                g.FonteGeradora, g.MedidasProtecaoExistentes,
                Funcoes = g.Funcoes.Select(f => new GheFuncaoDto(
                    f.FuncaoId,
                    _db.Funcoes.IgnoreQueryFilters().Where(x => x.Id == f.FuncaoId).Select(x => x.Nome).FirstOrDefault(),
                    _db.Funcoes.IgnoreQueryFilters().Where(x => x.Id == f.FuncaoId).Select(x => x.CboCodigo).FirstOrDefault(),
                    f.QuantidadeExpostos, f.DescricaoAtividades)).ToList(),
            })
            .ToListAsync(ct);

        var gheIds = ghes.Select(g => g.Id).ToList();
        var riscos = await _db.Riscos.AsNoTracking()
            .Join(_db.Atividades.Where(a => a.GheId != null && gheIds.Contains(a.GheId.Value)),
                r => r.AtividadeId, a => a.Id, (r, a) => new { r, a.GheId })
            .Select(x => new
            {
                x.GheId,
                Dto = new GheRiscoDto(
                    x.r.Id, x.r.AtividadeId,
                    _db.Perigos.IgnoreQueryFilters().Where(p => p.Id == x.r.PerigoId).Select(p => p.Nome).FirstOrDefault(),
                    _db.Perigos.IgnoreQueryFilters().Where(p => p.Id == x.r.PerigoId).Select(p => p.Agente).FirstOrDefault(),
                    x.r.Consequencia, x.r.Exposicao, x.r.Probabilidade, x.r.Severidade, x.r.NivelRisco,
                    x.r.ControlesExistentes, x.r.ControlesAdicionais, x.r.Status),
            })
            .ToListAsync(ct);

        var porGhe = riscos.ToLookup(x => x.GheId!.Value, x => x.Dto);
        return ghes.Select(g => new GheDto(
                g.Id, g.Numero, g.Setor, g.JornadaTrabalho, g.DescricaoAmbiente, g.AtividadesCriticas,
                g.FonteGeradora, g.MedidasProtecaoExistentes,
                g.Funcoes.OrderBy(f => f.FuncaoNome).ToList(),
                porGhe[g.Id].OrderByDescending(r => r.NivelRisco).ThenBy(r => r.Perigo).ToList()))
            .ToList();
    }
}

using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Catalogo;

// Catálogo de APR/PT (29/09): a "lista de modelos" é a própria lista de Atividades da obra com os
// Riscos do PGR — nada é cadastrado em duplicidade. Cada linha mostra o que já foi emitido para a
// atividade (APRs e PTs) e se existe documento vigente, para enxergar de uma vez as atividades da
// obra que estão sem APR/PT em dia.
public record ObterCatalogoAtividadesQuery(Guid? ObraId) : IRequest<List<CatalogoAtividadeDto>>;

public record CatalogoDocumentoDto(Guid Id, string? Numero, int Status, DateTime Data, DateTime? Validade, bool Vigente);

public record CatalogoAtividadeDto(
    Guid AtividadeId,
    string Nome,
    string? Descricao,
    Guid ObraId,
    string ObraNome,
    string? PgrNome,
    int QuantidadeRiscos,
    int? MaiorNivelRisco,
    List<CatalogoDocumentoDto> Aprs,
    List<CatalogoDocumentoDto> Pts);

public class ObterCatalogoAtividadesQueryHandler
    : IRequestHandler<ObterCatalogoAtividadesQuery, List<CatalogoAtividadeDto>>
{
    private const int LimiteDocumentosPorAtividade = 20;

    private readonly IAppDbContext _db;
    public ObterCatalogoAtividadesQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<CatalogoAtividadeDto>> Handle(ObterCatalogoAtividadesQuery request, CancellationToken ct)
    {
        var atividades = await _db.Atividades.AsNoTracking().EmUso(_db)
            .Where(a => request.ObraId == null || a.ObraId == request.ObraId)
            .Select(a => new { a.Id, a.Nome, a.Descricao, a.ObraId })
            .ToListAsync(ct);
        if (atividades.Count == 0) return new();

        var atividadeIds = atividades.Select(a => a.Id).ToList();
        var obraIds = atividades.Select(a => a.ObraId).Distinct().ToList();

        // Consultas separadas (sem projetar navegação): evita o INNER JOIN que some com a linha
        // quando o registro pai está excluído (soft delete).
        var obras = await _db.Obras.AsNoTracking()
            .Where(o => obraIds.Contains(o.Id))
            .Select(o => new { o.Id, o.Nome })
            .ToDictionaryAsync(o => o.Id, o => o.Nome, ct);

        var pgrs = await _db.Pgrs.AsNoTracking()
            .Where(p => obraIds.Contains(p.ObraId))
            .OrderByDescending(p => p.DataElaboracao)
            .Select(p => new { p.ObraId, p.Nome })
            .ToListAsync(ct);
        var pgrPorObra = pgrs.GroupBy(p => p.ObraId).ToDictionary(g => g.Key, g => g.First().Nome);

        var riscos = await _db.Riscos.AsNoTracking()
            .Where(r => atividadeIds.Contains(r.AtividadeId))
            .Select(r => new { r.AtividadeId, r.NivelRisco })
            .ToListAsync(ct);
        var riscosPorAtividade = riscos.GroupBy(r => r.AtividadeId).ToDictionary(g => g.Key, g => g.ToList());

        var hoje = DateTime.UtcNow.Date;

        var aprs = await _db.Aprs.AsNoTracking()
            .Where(a => atividadeIds.Contains(a.AtividadeId))
            .OrderByDescending(a => a.Data)
            .Select(a => new { a.Id, a.AtividadeId, a.NumeroApr, a.Status, a.Data, a.Validade })
            .ToListAsync(ct);
        var aprsPorAtividade = aprs.GroupBy(a => a.AtividadeId).ToDictionary(g => g.Key, g => g
            .Take(LimiteDocumentosPorAtividade)
            .Select(a => new CatalogoDocumentoDto(a.Id, a.NumeroApr, (int)a.Status, a.Data, a.Validade,
                a.Status == StatusApr.Aprovada && (a.Validade == null || a.Validade.Value.Date >= hoje)))
            .ToList());

        var pts = await _db.PermissoesTrabalho.AsNoTracking()
            .Where(p => atividadeIds.Contains(p.AtividadeId))
            .OrderByDescending(p => p.Data)
            .Select(p => new { p.Id, p.AtividadeId, p.NumeroPt, p.Status, p.Data, p.Validade })
            .ToListAsync(ct);
        var ptsPorAtividade = pts.GroupBy(p => p.AtividadeId).ToDictionary(g => g.Key, g => g
            .Take(LimiteDocumentosPorAtividade)
            .Select(p => new CatalogoDocumentoDto(p.Id, p.NumeroPt, (int)p.Status, p.Data, p.Validade,
                p.Status == StatusPt.Autorizada && (p.Validade == null || p.Validade.Value.Date >= hoje)))
            .ToList());

        return atividades
            .OrderBy(a => obras.GetValueOrDefault(a.ObraId, ""))
            .ThenBy(a => a.Nome)
            .Select(a =>
            {
                var rs = riscosPorAtividade.GetValueOrDefault(a.Id) ?? new();
                return new CatalogoAtividadeDto(
                    a.Id, a.Nome, a.Descricao, a.ObraId, obras.GetValueOrDefault(a.ObraId, ""),
                    pgrPorObra.GetValueOrDefault(a.ObraId),
                    rs.Count,
                    rs.Count == 0 ? null : rs.Max(r => (int)r.NivelRisco),
                    aprsPorAtividade.GetValueOrDefault(a.Id) ?? new(),
                    ptsPorAtividade.GetValueOrDefault(a.Id) ?? new());
            })
            .ToList();
    }
}

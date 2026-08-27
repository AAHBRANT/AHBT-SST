using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EstoquesEpi.Queries;

// Lista todo o catálogo de EPI com o saldo (0 quando não há linha de EstoqueEpi ainda) de uma Obra
// específica — base da nova tela de estoque por obra (Fase 3).
public record ListarEstoqueEpiPorObraQuery(Guid ObraId) : IRequest<List<EstoqueEpiPorObraDto>>;

public class ListarEstoqueEpiPorObraQueryHandler : IRequestHandler<ListarEstoqueEpiPorObraQuery, List<EstoqueEpiPorObraDto>>
{
    private readonly IAppDbContext _db;
    public ListarEstoqueEpiPorObraQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<EstoqueEpiPorObraDto>> Handle(ListarEstoqueEpiPorObraQuery request, CancellationToken ct)
        => await _db.CatalogoEpis
            .OrderBy(c => c.Nome)
            .Select(c => new EstoqueEpiPorObraDto(
                c.Id,
                c.Nome,
                c.Fabricante,
                c.Estoques.Where(e => e.ObraId == request.ObraId).Sum(e => (int?)e.Saldo) ?? 0))
            .ToListAsync(ct);
}

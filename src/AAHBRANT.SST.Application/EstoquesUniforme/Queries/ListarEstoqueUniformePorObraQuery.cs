using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EstoquesUniforme.Queries;

// Diferente de ListarEstoqueEpiPorObraQuery (que parte do catálogo fixo e mostra saldo=0 pros
// itens sem linha ainda): aqui não existe uma lista fechada de "todos os tamanhos possíveis" pra
// enumerar zerados — cada linha de EstoqueUniforme É um tamanho que já existe. Partir de
// EstoquesUniforme é o correto aqui.
public record ListarEstoqueUniformePorObraQuery(Guid ObraId) : IRequest<List<EstoqueUniformePorObraDto>>;

public class ListarEstoqueUniformePorObraQueryHandler : IRequestHandler<ListarEstoqueUniformePorObraQuery, List<EstoqueUniformePorObraDto>>
{
    private readonly IAppDbContext _db;
    public ListarEstoqueUniformePorObraQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<EstoqueUniformePorObraDto>> Handle(ListarEstoqueUniformePorObraQuery request, CancellationToken ct)
        => await _db.EstoquesUniforme
            .Where(e => e.ObraId == request.ObraId)
            .OrderBy(e => e.CatalogoUniforme!.Nome).ThenBy(e => e.Tamanho)
            .Select(e => new EstoqueUniformePorObraDto(e.CatalogoUniformeId, e.CatalogoUniforme!.Nome, e.Tamanho, e.Saldo))
            .ToListAsync(ct);
}

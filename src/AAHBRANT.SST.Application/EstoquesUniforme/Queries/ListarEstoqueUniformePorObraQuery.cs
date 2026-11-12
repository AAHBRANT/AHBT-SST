using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EstoquesUniforme.Queries;

public record ListarEstoqueUniformePorObraQuery(Guid ObraId) : IRequest<List<EstoqueUniformePorObraDto>>;

public class ListarEstoqueUniformePorObraQueryHandler : IRequestHandler<ListarEstoqueUniformePorObraQuery, List<EstoqueUniformePorObraDto>>
{
    private readonly IAppDbContext _db;
    public ListarEstoqueUniformePorObraQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<EstoqueUniformePorObraDto>> Handle(ListarEstoqueUniformePorObraQuery request, CancellationToken ct)
        => await (from c in _db.CatalogoUniformes
                  join e in _db.EstoquesUniforme.Where(e => e.ObraId == request.ObraId)
                      on c.Id equals e.CatalogoUniformeId into saldos
                  from e in saldos.DefaultIfEmpty()
                  orderby c.Nome, e.Tamanho
                  select new EstoqueUniformePorObraDto(c.Id, c.Nome, e == null ? string.Empty : e.Tamanho, e == null ? 0 : e.Saldo))
            .ToListAsync(ct);
}

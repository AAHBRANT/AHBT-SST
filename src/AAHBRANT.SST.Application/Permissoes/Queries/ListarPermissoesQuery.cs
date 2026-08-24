using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Permissoes.Queries;

// Catálogo de permissões: só leitura por design — não há Commands aqui. O catálogo é fixo,
// semeado na inicialização (um código por Módulo+Ação de cada módulo já implementado); ele
// só cresce quando um novo módulo é implementado no backend, nunca por edição manual via API,
// pois um `code` (ex.: "apr:aprovar") só faz sentido se algum endpoint realmente o verificar.
public record ListarPermissoesQuery(string? Modulo = null) : IRequest<List<PermissaoDto>>;

public class ListarPermissoesQueryHandler : IRequestHandler<ListarPermissoesQuery, List<PermissaoDto>>
{
    private readonly IAppDbContext _db;

    public ListarPermissoesQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<PermissaoDto>> Handle(ListarPermissoesQuery request, CancellationToken ct)
    {
        var query = _db.Permissoes.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Modulo))
            query = query.Where(p => p.Modulo == request.Modulo);

        return await query
            .OrderBy(p => p.Modulo).ThenBy(p => p.Acao)
            .Select(p => new PermissaoDto
            {
                Id = p.Id,
                Codigo = p.Codigo,
                Modulo = p.Modulo,
                Acao = p.Acao,
                Descricao = p.Descricao
            })
            .ToListAsync(ct);
    }
}

using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.TagsIdentificacao.Queries;

public record ListarTagsIdentificacaoQuery(StatusTag? Status = null, TipoTag? Tipo = null) : IRequest<List<TagIdentificacaoDto>>;

public class ListarTagsIdentificacaoQueryHandler : IRequestHandler<ListarTagsIdentificacaoQuery, List<TagIdentificacaoDto>>
{
    private readonly IAppDbContext _db;

    public ListarTagsIdentificacaoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<TagIdentificacaoDto>> Handle(ListarTagsIdentificacaoQuery request, CancellationToken ct)
    {
        var query = _db.TagsIdentificacao.AsQueryable();

        if (request.Status.HasValue)
            query = query.Where(t => t.Status == request.Status.Value);

        if (request.Tipo.HasValue)
            query = query.Where(t => t.Tipo == request.Tipo.Value);

        return await query
            .OrderBy(t => t.Uid)
            .Select(t => new TagIdentificacaoDto
            {
                Id = t.Id,
                Uid = t.Uid,
                Tipo = t.Tipo,
                Status = t.Status,
                EntidadeVinculadaTipo = t.EntidadeVinculadaTipo,
                EntidadeVinculadaId = t.EntidadeVinculadaId
            })
            .ToListAsync(ct);
    }
}

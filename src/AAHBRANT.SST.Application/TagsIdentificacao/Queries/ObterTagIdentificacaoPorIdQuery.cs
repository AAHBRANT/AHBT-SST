using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.TagsIdentificacao.Queries;

public record ObterTagIdentificacaoPorIdQuery(Guid Id) : IRequest<TagIdentificacaoDto?>;

public class ObterTagIdentificacaoPorIdQueryHandler : IRequestHandler<ObterTagIdentificacaoPorIdQuery, TagIdentificacaoDto?>
{
    private readonly IAppDbContext _db;

    public ObterTagIdentificacaoPorIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<TagIdentificacaoDto?> Handle(ObterTagIdentificacaoPorIdQuery request, CancellationToken ct)
    {
        return await _db.TagsIdentificacao
            .Where(t => t.Id == request.Id)
            .Select(t => new TagIdentificacaoDto
            {
                Id = t.Id,
                Uid = t.Uid,
                Tipo = t.Tipo,
                Status = t.Status,
                EntidadeVinculadaTipo = t.EntidadeVinculadaTipo,
                EntidadeVinculadaId = t.EntidadeVinculadaId
            })
            .FirstOrDefaultAsync(ct);
    }
}

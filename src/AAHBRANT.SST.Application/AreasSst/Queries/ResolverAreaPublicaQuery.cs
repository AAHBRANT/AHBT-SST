using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.AreasSst.Queries;

// NTAG.md §2/§3.B.4 — rota pública de leitura: aceita tanto o Código da Área (ex.: "AT-0017")
// quanto o Uid físico de uma Tag vinculada a essa Área, e resolve para o mesmo card público.
public record ResolverAreaPublicaQuery(string CodigoOuUid) : IRequest<AreaPublicaDto?>;

public class ResolverAreaPublicaQueryHandler : IRequestHandler<ResolverAreaPublicaQuery, AreaPublicaDto?>
{
    private readonly IAppDbContext _db;

    public ResolverAreaPublicaQueryHandler(IAppDbContext db) => _db = db;

    public async Task<AreaPublicaDto?> Handle(ResolverAreaPublicaQuery request, CancellationToken ct)
    {
        var area = await _db.AreasSst.FirstOrDefaultAsync(a => a.Codigo == request.CodigoOuUid, ct);

        if (area is null)
        {
            var tag = await _db.TagsIdentificacao.FirstOrDefaultAsync(t => t.Uid == request.CodigoOuUid, ct);
            if (tag is { EntidadeVinculadaTipo: TipoEntidadeVinculada.Area, EntidadeVinculadaId: not null })
                area = await _db.AreasSst.FirstOrDefaultAsync(a => a.Id == tag.EntidadeVinculadaId.Value, ct);
        }

        if (area is null) return null;

        return new AreaPublicaDto
        {
            Codigo = area.Codigo,
            Nome = area.Nome,
            Tipo = area.Tipo,
            Status = area.Status,
            Riscos = area.Riscos,
            Requisitos = area.Requisitos,
            DetalhesLocalizacao = area.DetalhesLocalizacao
        };
    }
}

using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.TagsIdentificacao.Queries;

// NTAG.md §1 — "Princípio Chave": a tag só guarda o identificador; o sistema resolve o Uid e
// carrega a entidade correspondente no contexto correto de SST. Esta query é essa resolução.
public record ResolverTagPorUidQuery(string Uid) : IRequest<ResolverTagDto?>;

public class ResolverTagPorUidQueryHandler : IRequestHandler<ResolverTagPorUidQuery, ResolverTagDto?>
{
    private readonly IAppDbContext _db;

    public ResolverTagPorUidQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ResolverTagDto?> Handle(ResolverTagPorUidQuery request, CancellationToken ct)
    {
        var tag = await _db.TagsIdentificacao.FirstOrDefaultAsync(t => t.Uid == request.Uid, ct);
        if (tag is null)
            return null;

        string? nomeEntidade = null;

        if (tag.EntidadeVinculadaTipo == TipoEntidadeVinculada.Area && tag.EntidadeVinculadaId.HasValue)
        {
            nomeEntidade = await _db.AreasSst
                .Where(a => a.Id == tag.EntidadeVinculadaId.Value)
                .Select(a => a.Nome)
                .FirstOrDefaultAsync(ct);
        }
        else if (tag.EntidadeVinculadaTipo == TipoEntidadeVinculada.Trabalhador && tag.EntidadeVinculadaId.HasValue)
        {
            nomeEntidade = await _db.Trabalhadores
                .Where(t => t.Id == tag.EntidadeVinculadaId.Value)
                .Select(t => t.Nome)
                .FirstOrDefaultAsync(ct);
        }
        else if (tag.EntidadeVinculadaTipo == TipoEntidadeVinculada.Ativo && tag.EntidadeVinculadaId.HasValue)
        {
            // Catálogo de ativos (Motor Central de Alertas + Cadastro de Ativos, 2026-08-25) — antes
            // desta entidade existir, só o Id cru era retornado (ver comentário original removido aqui).
            nomeEntidade = await _db.AtivosSst
                .Where(a => a.Id == tag.EntidadeVinculadaId.Value)
                .Select(a => a.Descricao + " — " + a.Identificacao)
                .FirstOrDefaultAsync(ct);
        }

        return new ResolverTagDto
        {
            TagId = tag.Id,
            Uid = tag.Uid,
            Tipo = tag.Tipo,
            Status = tag.Status,
            EntidadeVinculadaTipo = tag.EntidadeVinculadaTipo,
            EntidadeVinculadaId = tag.EntidadeVinculadaId,
            EntidadeVinculadaNome = nomeEntidade
        };
    }
}

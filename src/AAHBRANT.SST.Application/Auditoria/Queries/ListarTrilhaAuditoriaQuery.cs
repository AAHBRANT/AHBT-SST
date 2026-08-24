using AAHBRANT.SST.Application.Auditoria;
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Auditoria.Queries;

// Append-only por design — só leitura aqui, sem Commands (nenhum endpoint de escrita).
// A trilha em si é populada pelos próprios handlers de cada módulo (fora do escopo desta
// fatia: hoje só o esqueleto/entidade existe; nenhum handler ainda grava nela — ver aviso
// ao usuário sobre isso não ser "trilha em uso", só "trilha consultável").
public record ListarTrilhaAuditoriaQuery(
    string? EntidadeTipo = null,
    Guid? EntidadeId = null,
    Guid? UsuarioId = null,
    DateTime? DataInicio = null,
    DateTime? DataFim = null) : IRequest<List<TrilhaAuditoriaDto>>;

public class ListarTrilhaAuditoriaQueryHandler
    : IRequestHandler<ListarTrilhaAuditoriaQuery, List<TrilhaAuditoriaDto>>
{
    private readonly IAppDbContext _db;

    public ListarTrilhaAuditoriaQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<TrilhaAuditoriaDto>> Handle(ListarTrilhaAuditoriaQuery request, CancellationToken ct)
    {
        var query = _db.TrilhaAuditoria.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.EntidadeTipo))
            query = query.Where(t => t.EntidadeTipo == request.EntidadeTipo);
        if (request.EntidadeId.HasValue)
            query = query.Where(t => t.EntidadeId == request.EntidadeId.Value);
        if (request.UsuarioId.HasValue)
            query = query.Where(t => t.UsuarioId == request.UsuarioId.Value);
        if (request.DataInicio.HasValue)
            query = query.Where(t => t.Timestamp >= request.DataInicio.Value);
        if (request.DataFim.HasValue)
            query = query.Where(t => t.Timestamp <= request.DataFim.Value);

        return await query
            .OrderByDescending(t => t.Timestamp)
            .Select(t => new TrilhaAuditoriaDto
            {
                Id = t.Id,
                Timestamp = t.Timestamp,
                UsuarioId = t.UsuarioId,
                UsuarioNome = t.Usuario != null ? t.Usuario.Nome : null,
                Acao = t.Acao,
                EntidadeTipo = t.EntidadeTipo,
                EntidadeId = t.EntidadeId,
                DadosAntesJson = t.DadosAntesJson,
                DadosDepoisJson = t.DadosDepoisJson
            })
            .ToListAsync(ct);
    }
}

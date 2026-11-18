using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cameras.Queries;

// Nunca inclui o segredo nem o hash.
public record AgenteCameraDto(Guid Id, Guid ObraId, string ObraNome, string Nome, bool Ativo, string? Versao, DateTime? UltimoContatoEmUtc);

public record ListarAgentesCameraQuery(Guid? ObraId) : IRequest<List<AgenteCameraDto>>;

public class ListarAgentesCameraQueryHandler : IRequestHandler<ListarAgentesCameraQuery, List<AgenteCameraDto>>
{
    private readonly IAppDbContext _db;
    public ListarAgentesCameraQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<AgenteCameraDto>> Handle(ListarAgentesCameraQuery request, CancellationToken ct)
    {
        // O nome da obra vem de consulta à parte, sem o filtro de Ativo da Obra: com `a.Obra!.Nome` o INNER JOIN com a obra
        // inativada fazia o agente sumir da lista (e ele continuaria autenticando, sem como revogar). O escopo por obra
        // do usuário continua valendo: a lista de agentes já sai filtrada por ele.
        var agentes = await _db.AgentesCamera
            .Where(a => request.ObraId == null || a.ObraId == request.ObraId)
            .Select(a => new { a.Id, a.ObraId, a.Nome, a.Ativo, a.Versao, a.UltimoContatoEmUtc })
            .ToListAsync(ct);

        var obraIds = agentes.Select(a => a.ObraId).Distinct().ToList();
        var nomesDasObras = await _db.Obras.IgnoreQueryFilters()
            .Where(o => obraIds.Contains(o.Id))
            .Select(o => new { o.Id, o.Nome })
            .ToDictionaryAsync(o => o.Id, o => o.Nome, ct);
        string NomeDaObra(Guid id) => nomesDasObras.TryGetValue(id, out var nome) ? nome : string.Empty;
        agentes = agentes.OrderBy(a => NomeDaObra(a.ObraId), StringComparer.CurrentCultureIgnoreCase).ThenBy(a => a.Nome, StringComparer.CurrentCultureIgnoreCase).ToList();

        // datetime2 volta sem fuso (Kind Unspecified) e o JSON sairia sem "Z": marca como UTC, que é o que é.
        return agentes.Select(a => new AgenteCameraDto(a.Id, a.ObraId, NomeDaObra(a.ObraId), a.Nome, a.Ativo, a.Versao,
            a.UltimoContatoEmUtc is { } u ? DateTime.SpecifyKind(u, DateTimeKind.Utc) : null)).ToList();
    }
}

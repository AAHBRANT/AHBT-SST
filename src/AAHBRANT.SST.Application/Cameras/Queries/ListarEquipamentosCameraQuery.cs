using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cameras.Queries;

// Nunca inclui a senha (nem a cifrada): só o indicador SenhaDefinida.
public record EquipamentoCameraDto(
    Guid Id,
    Guid ObraId,
    string ObraNome,
    Guid AgenteCameraId,
    string AgenteNome,
    string Nome,
    string Host,
    int PortaRtsp,
    int PortaOnvif,
    string? EnderecoMac,
    string? Usuario,
    bool SenhaDefinida,
    string? Fabricante,
    string? Modelo,
    DateTime? UltimoIpDescobertoEmUtc,
    int QuantidadeCameras);

public record ListarEquipamentosCameraQuery(Guid? ObraId) : IRequest<List<EquipamentoCameraDto>>;

public class ListarEquipamentosCameraQueryHandler : IRequestHandler<ListarEquipamentosCameraQuery, List<EquipamentoCameraDto>>
{
    private readonly IAppDbContext _db;
    public ListarEquipamentosCameraQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<EquipamentoCameraDto>> Handle(ListarEquipamentosCameraQuery request, CancellationToken ct)
    {
        // Sem navegação no Select: o nome da obra e do agente vêm de consultas à parte, sem o filtro de Ativo (o INNER JOIN com
        // uma obra ou um agente inativado faria o equipamento sumir da lista). O escopo por obra do usuário já vale na lista.
        var equipamentos = await _db.EquipamentosCamera.AsNoTracking()
            .Where(e => request.ObraId == null || e.ObraId == request.ObraId)
            .Select(e => new
            {
                e.Id,
                e.ObraId,
                e.AgenteCameraId,
                e.Nome,
                e.Host,
                e.PortaRtsp,
                e.PortaOnvif,
                e.EnderecoMac,
                e.Usuario,
                SenhaDefinida = e.SenhaCifrada != null && e.SenhaCifrada != string.Empty,
                e.Fabricante,
                e.Modelo,
                e.UltimoIpDescobertoEmUtc,
                QuantidadeCameras = _db.Cameras.Count(c => c.EquipamentoCameraId == e.Id),
            })
            .ToListAsync(ct);

        var obraIds = equipamentos.Select(e => e.ObraId).Distinct().ToList();
        var nomesDasObras = await _db.Obras.IgnoreQueryFilters().AsNoTracking()
            .Where(o => obraIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => o.Nome, ct);
        var agenteIds = equipamentos.Select(e => e.AgenteCameraId).Distinct().ToList();
        var nomesDosAgentes = await _db.AgentesCamera.IgnoreQueryFilters().AsNoTracking()
            .Where(a => agenteIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Nome, ct);

        string Obra(Guid id) => nomesDasObras.TryGetValue(id, out var nome) ? nome : string.Empty;
        string Agente(Guid id) => nomesDosAgentes.TryGetValue(id, out var nome) ? nome : string.Empty;

        return equipamentos
            .OrderBy(e => Obra(e.ObraId), StringComparer.CurrentCultureIgnoreCase).ThenBy(e => e.Nome, StringComparer.CurrentCultureIgnoreCase)
            .Select(e => new EquipamentoCameraDto(
                e.Id, e.ObraId, Obra(e.ObraId), e.AgenteCameraId, Agente(e.AgenteCameraId), e.Nome, e.Host, e.PortaRtsp, e.PortaOnvif,
                e.EnderecoMac, e.Usuario, e.SenhaDefinida, e.Fabricante, e.Modelo,
                e.UltimoIpDescobertoEmUtc is { } u ? DateTime.SpecifyKind(u, DateTimeKind.Utc) : null,
                e.QuantidadeCameras))
            .ToList();
    }
}

using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cameras.Queries;

public record ListarCamerasQuery(Guid? ObraId) : IRequest<List<CameraDto>>;

public record CameraDto(
    Guid Id,
    Guid ObraId,
    string ObraNome,
    string Nome,
    Guid? AreaSstId,
    string? AreaNome,
    TipoCamera Tipo,
    bool Habilitada,
    int IntervaloMinimoAlertasMinutos,
    DateTime? UltimaAnaliseEmUtc,
    int OcorrenciasHoje);

public class ListarCamerasQueryHandler : IRequestHandler<ListarCamerasQuery, List<CameraDto>>
{
    private readonly IAppDbContext _db;
    public ListarCamerasQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<CameraDto>> Handle(ListarCamerasQuery request, CancellationToken ct)
    {
        // "Hoje" aproximado pelas últimas 24 h: evita depender de fuso na consulta.
        var desde = DateTime.UtcNow.AddHours(-24);

        var query = _db.Cameras.AsNoTracking();
        if (request.ObraId is { } obraId) query = query.Where(c => c.ObraId == obraId);

        return await query
            .OrderBy(c => c.Nome)
            .Select(c => new CameraDto(
                c.Id,
                c.ObraId,
                c.Obra!.Nome,
                c.Nome,
                c.AreaSstId,
                c.AreaSst != null ? c.AreaSst.Nome : null,
                c.Tipo,
                c.Habilitada,
                c.IntervaloMinimoAlertasMinutos,
                c.UltimaAnaliseEmUtc,
                _db.OcorrenciasEpiCamera.Count(o => o.CameraId == c.Id && o.RecebidaEmUtc >= desde)))
            .ToListAsync(ct);
    }
}

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
    int OcorrenciasHoje,
    int TotalAnalises,
    // Consumo real da IA (tokens informados pela API a cada análise) e o custo calculado em dólares
    // (CustoIaCameras). "NoDia" vale para DiaDoConsumo, a data em Brasília da última análise.
    long TotalTokensEntrada = 0,
    long TotalTokensSaida = 0,
    decimal CustoTotalUsd = 0,
    DateTime? DiaDoConsumo = null,
    int AnalisesNoDia = 0,
    decimal CustoNoDiaUsd = 0);

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

        var cameras = await query
            .OrderBy(c => c.Nome)
            .Select(c => new
            {
                c.Id,
                c.ObraId,
                ObraNome = c.Obra!.Nome,
                c.Nome,
                c.AreaSstId,
                AreaNome = c.AreaSst != null ? c.AreaSst.Nome : null,
                c.Tipo,
                c.Habilitada,
                c.IntervaloMinimoAlertasMinutos,
                c.UltimaAnaliseEmUtc,
                OcorrenciasHoje = _db.OcorrenciasEpiCamera.Count(o => o.CameraId == c.Id && o.RecebidaEmUtc >= desde),
                c.TotalAnalises,
                c.TotalTokensEntrada,
                c.TotalTokensSaida,
                c.DiaDoConsumo,
                c.AnalisesNoDia,
                c.TokensEntradaNoDia,
                c.TokensSaidaNoDia,
            })
            .ToListAsync(ct);

        // O custo é calculado fora da consulta (decimal com arredondamento não traduz para SQL).
        return cameras.Select(c => new CameraDto(
            c.Id,
            c.ObraId,
            c.ObraNome,
            c.Nome,
            c.AreaSstId,
            c.AreaNome,
            c.Tipo,
            c.Habilitada,
            c.IntervaloMinimoAlertasMinutos,
            c.UltimaAnaliseEmUtc,
            c.OcorrenciasHoje,
            c.TotalAnalises,
            c.TotalTokensEntrada,
            c.TotalTokensSaida,
            CustoIaCameras.EmDolares(c.TotalTokensEntrada, c.TotalTokensSaida),
            c.DiaDoConsumo,
            c.AnalisesNoDia,
            CustoIaCameras.EmDolares(c.TokensEntradaNoDia, c.TokensSaidaNoDia))).ToList();
    }
}

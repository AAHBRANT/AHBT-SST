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
    decimal CustoNoDiaUsd = 0,
    // Câmera IP (Fonte = Rtsp): equipamento, lente e qualidade. SituacaoConexao vem calculada (ver SituacaoConexaoCameraRegras):
    // nula para câmera do navegador ou desligada.
    FonteCamera Fonte = FonteCamera.Navegador,
    Guid? EquipamentoCameraId = null,
    string? EquipamentoNome = null,
    int? Canal = null,
    int? Stream = null,
    int IntervaloEnvioSegundos = CameraIpRegras.IntervaloEnvioPadraoSegundos,
    string? SituacaoConexao = null,
    DateTime? UltimoQuadroRecebidoEmUtc = null,
    string? MensagemConexao = null);

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
                c.Fonte,
                c.EquipamentoCameraId,
                c.Canal,
                c.Stream,
                c.IntervaloEnvioSegundos,
                c.UltimoQuadroRecebidoEmUtc,
                c.MensagemConexao,
            })
            .ToListAsync(ct);

        // Equipamento e agente vêm de consultas à parte (e não de navegação no Select): o INNER JOIN com um equipamento ou
        // agente inativado faria a câmera sumir da lista. Só os ids das câmeras que o usuário já enxerga entram aqui.
        var equipamentoIds = cameras.Where(c => c.EquipamentoCameraId != null).Select(c => c.EquipamentoCameraId!.Value).Distinct().ToList();
        var equipamentos = equipamentoIds.Count == 0
            ? new Dictionary<Guid, (string Nome, Guid AgenteId)>()
            : await _db.EquipamentosCamera.IgnoreQueryFilters().AsNoTracking()
                .Where(e => equipamentoIds.Contains(e.Id))
                .Select(e => new { e.Id, e.Nome, e.AgenteCameraId })
                .ToDictionaryAsync(e => e.Id, e => (Nome: e.Nome, AgenteId: e.AgenteCameraId), ct);
        var agenteIds = equipamentos.Values.Select(e => e.AgenteId).Distinct().ToList();
        var agentes = agenteIds.Count == 0
            ? new Dictionary<Guid, (bool Ativo, DateTime? UltimoContato)>()
            : await _db.AgentesCamera.IgnoreQueryFilters().AsNoTracking()
                .Where(a => agenteIds.Contains(a.Id))
                .Select(a => new { a.Id, a.Ativo, a.UltimoContatoEmUtc })
                .ToDictionaryAsync(a => a.Id, a => (Ativo: a.Ativo, UltimoContato: a.UltimoContatoEmUtc), ct);
        var agora = DateTime.UtcNow;

        string? Situacao(FonteCamera fonte, bool habilitada, Guid? equipamentoId, DateTime? ultimoQuadro)
        {
            var temAgente = equipamentoId is { } eid && equipamentos.TryGetValue(eid, out var eq) && agentes.ContainsKey(eq.AgenteId);
            var agente = temAgente ? agentes[equipamentos[equipamentoId!.Value].AgenteId] : default;
            return SituacaoConexaoCameraRegras.Calcular(fonte, habilitada, temAgente && agente.Ativo, agente.UltimoContato, ultimoQuadro, agora);
        }

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
            CustoIaCameras.EmDolares(c.TokensEntradaNoDia, c.TokensSaidaNoDia),
            c.Fonte,
            c.EquipamentoCameraId,
            c.EquipamentoCameraId is { } id && equipamentos.TryGetValue(id, out var equipamento) ? equipamento.Nome : null,
            c.Canal,
            c.Stream,
            c.IntervaloEnvioSegundos,
            Situacao(c.Fonte, c.Habilitada, c.EquipamentoCameraId, c.UltimoQuadroRecebidoEmUtc),
            // datetime2 volta sem fuso (Kind Unspecified) e o JSON sairia sem "Z": marca como UTC, que é o que é.
            c.UltimoQuadroRecebidoEmUtc is { } uq ? DateTime.SpecifyKind(uq, DateTimeKind.Utc) : null,
            c.MensagemConexao)).ToList();
    }
}

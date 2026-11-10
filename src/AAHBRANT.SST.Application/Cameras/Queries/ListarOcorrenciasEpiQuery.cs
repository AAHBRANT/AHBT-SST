using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cameras.Queries;

public record ListarOcorrenciasEpiQuery(
    Guid? ObraId,
    Guid? CameraId,
    DateTime? DeUtc,
    DateTime? AteUtc,
    SituacaoOcorrenciaEpi? Situacao) : IRequest<List<OcorrenciaEpiDto>>;

// Sem a foto: a lista não pode carregar os bytes de até 200 imagens. A foto vem por
// ObterFotoOcorrenciaEpiQuery, uma por vez.
public record OcorrenciaEpiDto(
    Guid Id,
    Guid CameraId,
    string CameraNome,
    Guid ObraId,
    string ObraNome,
    DateTime CapturadaEmUtc,
    DateTime RecebidaEmUtc,
    int PessoasNaCena,
    int SemCapacete,
    int SemColete,
    ConfiancaAnaliseEpi Confianca,
    bool AlertaEnviado,
    Guid? TrabalhadorId,
    string? TrabalhadorNome,
    SituacaoOcorrenciaEpi Situacao,
    // true = trabalhador apontado pelo reconhecimento facial, ainda sem conferência do técnico.
    bool IdentificadoPorRosto = false,
    double? ConfiancaIdentificacao = null);

public class ListarOcorrenciasEpiQueryHandler : IRequestHandler<ListarOcorrenciasEpiQuery, List<OcorrenciaEpiDto>>
{
    public const int LimiteItens = 200;

    private readonly IAppDbContext _db;
    public ListarOcorrenciasEpiQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<OcorrenciaEpiDto>> Handle(ListarOcorrenciasEpiQuery request, CancellationToken ct)
    {
        var query = _db.OcorrenciasEpiCamera.AsNoTracking();
        if (request.ObraId is { } obraId) query = query.Where(o => o.ObraId == obraId);
        if (request.CameraId is { } cameraId) query = query.Where(o => o.CameraId == cameraId);
        if (request.DeUtc is { } de) query = query.Where(o => o.RecebidaEmUtc >= de);
        if (request.AteUtc is { } ate) query = query.Where(o => o.RecebidaEmUtc <= ate);
        if (request.Situacao is { } situacao) query = query.Where(o => o.Situacao == situacao);

        // A foto não entra na projeção. O escopo por obra vem do filtro da própria ocorrência.
        var itens = await query
            .OrderByDescending(o => o.RecebidaEmUtc)
            .Take(LimiteItens)
            .Select(o => new
            {
                o.Id, o.CameraId, o.ObraId, o.CapturadaEmUtc, o.RecebidaEmUtc, o.PessoasNaCena,
                o.SemCapacete, o.SemColete, o.Confianca, o.AlertaEnviado, o.TrabalhadorId, o.Situacao,
                o.IdentificadoPorRosto, o.ConfiancaIdentificacao,
            })
            .ToListAsync(ct);

        // Nomes buscados com IgnoreQueryFilters: a ocorrência precisa continuar na lista mesmo se a
        // câmera foi excluída ou o trabalhador foi desligado.
        var cameraIds = itens.Select(i => i.CameraId).Distinct().ToList();
        var obraIds = itens.Select(i => i.ObraId).Distinct().ToList();
        var trabalhadorIds = itens.Where(i => i.TrabalhadorId != null).Select(i => i.TrabalhadorId!.Value).Distinct().ToList();

        var cameras = await _db.Cameras.IgnoreQueryFilters().AsNoTracking()
            .Where(c => cameraIds.Contains(c.Id)).Select(c => new { c.Id, c.Nome }).ToDictionaryAsync(c => c.Id, c => c.Nome, ct);
        var obras = await _db.Obras.IgnoreQueryFilters().AsNoTracking()
            .Where(o => obraIds.Contains(o.Id)).Select(o => new { o.Id, o.Nome }).ToDictionaryAsync(o => o.Id, o => o.Nome, ct);
        var trabalhadores = await _db.Trabalhadores.IgnoreQueryFilters().AsNoTracking()
            .Where(t => trabalhadorIds.Contains(t.Id)).Select(t => new { t.Id, t.Nome }).ToDictionaryAsync(t => t.Id, t => t.Nome, ct);

        return itens.Select(o => new OcorrenciaEpiDto(
            o.Id,
            o.CameraId,
            cameras.GetValueOrDefault(o.CameraId) ?? "-",
            o.ObraId,
            obras.GetValueOrDefault(o.ObraId) ?? "-",
            o.CapturadaEmUtc,
            o.RecebidaEmUtc,
            o.PessoasNaCena,
            o.SemCapacete,
            o.SemColete,
            o.Confianca,
            o.AlertaEnviado,
            o.TrabalhadorId,
            o.TrabalhadorId is { } tid ? trabalhadores.GetValueOrDefault(tid) : null,
            o.Situacao,
            o.IdentificadoPorRosto,
            o.ConfiancaIdentificacao)).ToList();
    }
}

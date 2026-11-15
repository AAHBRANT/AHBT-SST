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
    SituacaoOcorrenciaEpi? Situacao,
    TipoOcorrenciaEpi? Tipo = null) : IRequest<List<OcorrenciaEpiDto>>;

// Filtro "Tipo" da aba de alertas: o que motivou a ocorrência. Cor = aviso de capacete de cor diferente da função e
// também ocorrência irregular em que a cor vista difere da esperada (as duas cores conhecidas).
public enum TipoOcorrenciaEpi
{
    Capacete = 1,
    Colete = 2,
    Oculos = 3,
    Cor = 4,
}

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
    double? ConfiancaIdentificacao = null,
    // Repetições da mesma irregularidade somadas nesta ocorrência (1 = vista uma única vez) e a última vez.
    int VezesVista = 1,
    DateTime? UltimaVezEmUtc = null,
    int SemOculos = 0,
    // Cor vista pela câmera e a esperada para a função (nulo = não distinguida / sem identificação / função sem cor).
    CorCapacete? CorCapaceteVista = null,
    CorCapacete? CorCapaceteEsperada = null,
    // true = ocorrência só de cor do capacete diferente da função (gravidade menor, sem Telegram).
    bool AvisoCorCapacete = false,
    // Função do trabalhador, quando identificado.
    string? FuncaoNome = null,
    // Pessoas da ocorrência cujo item não pôde ser julgado (informativo; 0 em ocorrências antigas).
    int CapaceteNaoAvaliavel = 0,
    int ColeteNaoAvaliavel = 0,
    int OculosNaoAvaliavel = 0);

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
        if (request.Tipo is { } tipo)
            query = tipo switch
            {
                TipoOcorrenciaEpi.Capacete => query.Where(o => o.SemCapacete > 0),
                TipoOcorrenciaEpi.Colete => query.Where(o => o.SemColete > 0),
                TipoOcorrenciaEpi.Oculos => query.Where(o => o.SemOculos > 0),
                TipoOcorrenciaEpi.Cor => query.Where(o => o.AvisoCorCapacete
                    || (o.CorCapaceteVista != null && o.CorCapaceteEsperada != null && o.CorCapaceteVista != o.CorCapaceteEsperada)),
                _ => query,
            };

        // A foto não entra na projeção. O escopo por obra vem do filtro da própria ocorrência.
        var itens = await query
            .OrderByDescending(o => o.RecebidaEmUtc)
            .Take(LimiteItens)
            .Select(o => new
            {
                o.Id, o.CameraId, o.ObraId, o.CapturadaEmUtc, o.RecebidaEmUtc, o.PessoasNaCena,
                o.SemCapacete, o.SemColete, o.Confianca, o.AlertaEnviado, o.TrabalhadorId, o.Situacao,
                o.IdentificadoPorRosto, o.ConfiancaIdentificacao, o.VezesVista, o.UltimaVezEmUtc,
                o.SemOculos, o.CorCapaceteVista, o.CorCapaceteEsperada, o.AvisoCorCapacete,
                o.CapaceteNaoAvaliavel, o.ColeteNaoAvaliavel, o.OculosNaoAvaliavel,
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
            .Where(t => trabalhadorIds.Contains(t.Id)).Select(t => new { t.Id, t.Nome, t.FuncaoId }).ToListAsync(ct);
        // Função em consulta própria (sem JOIN): função excluída continua aparecendo no histórico.
        var funcaoIds = trabalhadores.Select(t => t.FuncaoId).Distinct().ToList();
        var funcoes = await _db.Funcoes.IgnoreQueryFilters().AsNoTracking()
            .Where(f => funcaoIds.Contains(f.Id)).Select(f => new { f.Id, f.Nome }).ToDictionaryAsync(f => f.Id, f => f.Nome, ct);
        var trabalhadorPorId = trabalhadores.ToDictionary(t => t.Id);

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
            o.TrabalhadorId is { } tid && trabalhadorPorId.TryGetValue(tid, out var trab) ? trab.Nome : null,
            o.Situacao,
            o.IdentificadoPorRosto,
            o.ConfiancaIdentificacao,
            o.VezesVista,
            o.UltimaVezEmUtc,
            o.SemOculos,
            o.CorCapaceteVista,
            o.CorCapaceteEsperada,
            o.AvisoCorCapacete,
            o.TrabalhadorId is { } fid && trabalhadorPorId.TryGetValue(fid, out var trabFunc) ? funcoes.GetValueOrDefault(trabFunc.FuncaoId) : null,
            o.CapaceteNaoAvaliavel,
            o.ColeteNaoAvaliavel,
            o.OculosNaoAvaliavel)).ToList();
    }
}

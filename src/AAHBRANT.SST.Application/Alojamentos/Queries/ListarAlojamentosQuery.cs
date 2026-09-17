using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alojamentos.Queries;

public record ListarAlojamentosQuery(Guid? ObraId) : IRequest<List<AlojamentoResumoDto>>;

public record AlojamentoResumoDto(
    Guid Id,
    Guid ObraId,
    string Nome,
    string? Endereco,
    int TotalMoradores,
    string StatusUltimaInspecao,
    int? DiasDesdeUltimaInspecao,
    AlojamentoInspecaoResumoDto? InspecaoEmAndamento,
    AlojamentoInspecaoResumoDto? UltimaInspecaoConcluida,
    List<AlojamentoInspecaoResumoDto> HistoricoInspecoes);

public record AlojamentoInspecaoResumoDto(
    Guid Id,
    DateTime Data,
    StatusInspecao Status,
    int TotalItens,
    int ItensRespondidos,
    int ItensNaoConformes,
    AlojamentoDocumentoAssinaturaResumoDto? DocumentoAssinatura);

public record AlojamentoDocumentoAssinaturaResumoDto(
    Guid Id,
    StatusDocumentoAssinatura Status,
    bool TemPdf,
    DateTime? FinalizadoEm);

public class ListarAlojamentosQueryHandler : IRequestHandler<ListarAlojamentosQuery, List<AlojamentoResumoDto>>
{
    private readonly IAppDbContext _db;
    public ListarAlojamentosQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<AlojamentoResumoDto>> Handle(ListarAlojamentosQuery request, CancellationToken ct)
    {
        var diasParaAtraso = (await _db.ConfiguracoesAlojamento.FirstOrDefaultAsync(ct))?.DiasParaInspecaoAtrasada ?? 30;
        var hoje = DateTime.UtcNow;

        var query = _db.Alojamentos.AsNoTracking();
        if (request.ObraId is { } obraId) query = query.Where(a => a.ObraId == obraId);

        var alojamentos = await query
            .Select(a => new
            {
                a.Id,
                a.ObraId,
                a.Nome,
                a.Endereco,
                TotalMoradores = a.Moradores.Count(m => m.DataSaida == null),
                Inspecoes = _db.Inspecoes
                    .Where(i => i.AlojamentoId == a.Id)
                    .OrderByDescending(i => i.Data)
                    .Select(i => new AlojamentoInspecaoResumoDto(
                        i.Id,
                        i.Data,
                        i.Status,
                        i.Respostas.Count(r => r.Ativo),
                        i.Respostas.Count(r => r.Ativo && r.StatusItem != null),
                        i.Respostas.Count(r => r.Ativo && r.StatusItem == StatusItemChecklist.NaoConforme),
                        _db.DocumentosAssinatura
                            .Where(d => d.EntidadeTipo == nameof(AAHBRANT.SST.Domain.Entidades.Inspecao) && d.EntidadeId == i.Id)
                            .Select(d => new AlojamentoDocumentoAssinaturaResumoDto(
                                d.Id,
                                d.Status,
                                d.PdfConteudo != null,
                                d.FinalizadoEm))
                            .FirstOrDefault()))
                    .ToList(),
            })
            .ToListAsync(ct);

        return alojamentos.Select(a =>
        {
            var inspecaoEmAndamento = a.Inspecoes.FirstOrDefault(i => i.Status == StatusInspecao.EmAndamento);
            var ultimaConcluida = a.Inspecoes.FirstOrDefault(i => i.Status == StatusInspecao.Concluida);
            if (ultimaConcluida is null)
                return new AlojamentoResumoDto(
                    a.Id, a.ObraId, a.Nome, a.Endereco, a.TotalMoradores, "nunca", null,
                    inspecaoEmAndamento, null, a.Inspecoes);

            var dias = (int)(hoje - ultimaConcluida.Data).TotalDays;
            var status = dias > diasParaAtraso ? "atrasada" : "em-dia";
            return new AlojamentoResumoDto(
                a.Id, a.ObraId, a.Nome, a.Endereco, a.TotalMoradores, status, dias,
                inspecaoEmAndamento, ultimaConcluida, a.Inspecoes);
        }).ToList();
    }
}

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
    int? DiasDesdeUltimaInspecao);

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
                UltimaInspecaoData = _db.Inspecoes
                    .Where(i => i.AlojamentoId == a.Id && i.Status == StatusInspecao.Concluida)
                    .OrderByDescending(i => i.Data)
                    .Select(i => (DateTime?)i.Data)
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        return alojamentos.Select(a =>
        {
            if (a.UltimaInspecaoData is not { } ultima)
                return new AlojamentoResumoDto(a.Id, a.ObraId, a.Nome, a.Endereco, a.TotalMoradores, "nunca", null);

            var dias = (int)(hoje - ultima).TotalDays;
            var status = dias > diasParaAtraso ? "atrasada" : "em-dia";
            return new AlojamentoResumoDto(a.Id, a.ObraId, a.Nome, a.Endereco, a.TotalMoradores, status, dias);
        }).ToList();
    }
}

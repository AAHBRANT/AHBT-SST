using AAHBRANT.SST.Application.Alojamentos.Queries;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Inspecoes;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Inspecoes.Temas;

// O resumo de cada inspeção reaproveita AlojamentoInspecaoResumoDto (mesmos campos que Veículos já usa):
// data, status, itens respondidos/NC e documento de assinatura.
// StatusUltimaInspecao: "nunca" (sem inspeção concluída) ou "inspecionado" (há uma concluída).
public record ItemInspecionavelResumoDto(
    Guid Id,
    Guid TemaInspecaoId,
    Guid ObraId,
    string ObraNome,
    string Identificacao,
    string? Descricao,
    string StatusUltimaInspecao,
    int? DiasDesdeUltimaInspecao,
    AlojamentoInspecaoResumoDto? InspecaoEmAndamento,
    AlojamentoInspecaoResumoDto? UltimaInspecaoConcluida,
    List<AlojamentoInspecaoResumoDto> HistoricoInspecoes,
    int? PeriodicidadeInspecaoDias = null,
    Guid? ResponsavelInspecaoUsuarioId = null,
    string? ResponsavelInspecaoNome = null,
    DateTime? ProximaInspecaoEm = null);

public record ListarItensInspecionaveisQuery(Guid TemaInspecaoId, Guid? ObraId) : IRequest<List<ItemInspecionavelResumoDto>>;

public class ListarItensInspecionaveisQueryHandler : IRequestHandler<ListarItensInspecionaveisQuery, List<ItemInspecionavelResumoDto>>
{
    private readonly IAppDbContext _db;
    public ListarItensInspecionaveisQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<ItemInspecionavelResumoDto>> Handle(ListarItensInspecionaveisQuery request, CancellationToken ct)
    {
        var hoje = DateTime.UtcNow;

        var query = _db.ItensInspecionaveis.AsNoTracking().Where(i => i.TemaInspecaoId == request.TemaInspecaoId);
        if (request.ObraId is { } obraId) query = query.Where(i => i.ObraId == obraId);

        var itens = await query
            .OrderBy(i => i.Identificacao)
            .Select(i => new
            {
                i.Id,
                i.TemaInspecaoId,
                i.ObraId,
                ObraNome = i.Obra != null ? i.Obra.Nome : string.Empty,
                i.Identificacao,
                i.Descricao,
                i.PeriodicidadeInspecaoDias,
                i.ResponsavelInspecaoUsuarioId,
                ResponsavelInspecaoNome = i.ResponsavelInspecaoUsuario != null ? i.ResponsavelInspecaoUsuario.Nome : null,
                i.CreatedAtUtc,
                Inspecoes = _db.Inspecoes
                    .Where(x => x.ItemInspecionavelId == i.Id)
                    .OrderByDescending(x => x.Data)
                    .Select(x => new AlojamentoInspecaoResumoDto(
                        x.Id,
                        x.Data,
                        x.Status,
                        x.Respostas.Count(r => r.Ativo),
                        x.Respostas.Count(r => r.Ativo && r.StatusItem != null),
                        x.Respostas.Count(r => r.Ativo && r.StatusItem == StatusItemChecklist.NaoConforme),
                        _db.DocumentosAssinatura
                            .Where(d => d.EntidadeTipo == nameof(AAHBRANT.SST.Domain.Entidades.Inspecao) && d.EntidadeId == x.Id)
                            .Select(d => new AlojamentoDocumentoAssinaturaResumoDto(
                                d.Id,
                                d.Status,
                                d.PdfConteudo != null,
                                d.FinalizadoEm))
                            .FirstOrDefault()))
                    .ToList(),
            })
            .ToListAsync(ct);

        return itens.Select(i =>
        {
            var emAndamento = i.Inspecoes.FirstOrDefault(x => x.Status == StatusInspecao.EmAndamento);
            var ultimaConcluida = i.Inspecoes.FirstOrDefault(x => x.Status == StatusInspecao.Concluida);
            return new ItemInspecionavelResumoDto(
                i.Id, i.TemaInspecaoId, i.ObraId, i.ObraNome, i.Identificacao, i.Descricao,
                ultimaConcluida is null ? "nunca" : "inspecionado",
                ultimaConcluida is null ? null : (int)(hoje - ultimaConcluida.Data).TotalDays,
                emAndamento, ultimaConcluida, i.Inspecoes,
                i.PeriodicidadeInspecaoDias, i.ResponsavelInspecaoUsuarioId, i.ResponsavelInspecaoNome,
                PeriodicidadeInspecao.ProximaEm(i.PeriodicidadeInspecaoDias, ultimaConcluida?.Data, i.CreatedAtUtc));
        }).ToList();
    }
}

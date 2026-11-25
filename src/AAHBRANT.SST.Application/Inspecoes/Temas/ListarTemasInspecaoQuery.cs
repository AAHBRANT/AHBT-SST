using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Inspecoes.Temas;

// Um card por tema na tela de Inspeções. TotalItens respeita o escopo de obra do usuário (filtro
// global de ItemInspecionavel); ChecklistModeloId é o checklist vigente do tema.
public record TemaInspecaoResumoDto(
    Guid Id,
    string Nome,
    int TotalItens,
    int ItensComInspecaoEmAndamento,
    int TotalInspecoes,
    Guid? ChecklistModeloId,
    int TotalItensChecklist,
    int? PeriodicidadeDiasPadrao = null,
    // Ponto de prazo no card (25/11/2026): itens com inspeção periódica vencendo em até 7 dias e
    // itens que já passaram da data. Sem periodicidade, o item não entra em nenhum dos dois.
    int ItensVencendo = 0,
    int ItensForaDoPrazo = 0);

public record ListarTemasInspecaoQuery : IRequest<List<TemaInspecaoResumoDto>>;

public class ListarTemasInspecaoQueryHandler : IRequestHandler<ListarTemasInspecaoQuery, List<TemaInspecaoResumoDto>>
{
    private readonly IAppDbContext _db;
    public ListarTemasInspecaoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<TemaInspecaoResumoDto>> Handle(ListarTemasInspecaoQuery request, CancellationToken ct)
    {
        var temas = await _db.TemasInspecao.AsNoTracking().OrderBy(t => t.Nome).Select(t => new { t.Id, t.Nome, t.PeriodicidadeDiasPadrao }).ToListAsync(ct);
        var itens = await _db.ItensInspecionaveis.AsNoTracking()
            .Select(i => new { i.Id, i.TemaInspecaoId, i.PeriodicidadeInspecaoDias, i.CreatedAtUtc })
            .ToListAsync(ct);
        var inspecoes = await _db.Inspecoes.AsNoTracking()
            .Where(i => i.ItemInspecionavelId != null)
            .Select(i => new { ItemId = i.ItemInspecionavelId!.Value, i.Status, i.Data })
            .ToListAsync(ct);
        var hoje = DateTime.UtcNow.Date;
        // Próxima inspeção de cada item: mesma regra da lista de itens (ListarItensInspecionaveisQuery).
        var proximaPorItem = itens.ToDictionary(
            i => i.Id,
            i => PeriodicidadeInspecao.ProximaEm(
                i.PeriodicidadeInspecaoDias,
                inspecoes.Where(x => x.ItemId == i.Id && x.Status == StatusInspecao.Concluida).Select(x => (DateTime?)x.Data).Max(),
                i.CreatedAtUtc));
        var checklists = await _db.ChecklistModelos.AsNoTracking()
            .Where(c => c.TipoInspecao == TipoInspecao.Tema && c.TemaInspecaoId != null)
            .Select(c => new { c.Id, TemaId = c.TemaInspecaoId!.Value, c.Versao, Itens = c.Itens.Count(i => i.Ativo) })
            .ToListAsync(ct);

        return temas.Select(t =>
        {
            var idsDoTema = itens.Where(i => i.TemaInspecaoId == t.Id).Select(i => i.Id).ToHashSet();
            var doTema = inspecoes.Where(i => idsDoTema.Contains(i.ItemId)).ToList();
            var vigente = checklists.Where(c => c.TemaId == t.Id).OrderByDescending(c => c.Versao).FirstOrDefault();
            return new TemaInspecaoResumoDto(
                t.Id, t.Nome, idsDoTema.Count,
                doTema.Where(i => i.Status == StatusInspecao.EmAndamento).Select(i => i.ItemId).Distinct().Count(),
                doTema.Count,
                vigente?.Id, vigente?.Itens ?? 0, t.PeriodicidadeDiasPadrao,
                ItensVencendo: idsDoTema.Count(id => proximaPorItem[id] is { } p && p.Date >= hoje && p.Date <= hoje.AddDays(7)),
                ItensForaDoPrazo: idsDoTema.Count(id => proximaPorItem[id] is { } p && p.Date < hoje));
        }).ToList();
    }
}

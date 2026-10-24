using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alertas.Motor;

public class AsoAlertaProvider : IAlertaOrigemProvider
{
    private readonly IAppDbContext _db;

    public TipoModuloAlerta Modulo => TipoModuloAlerta.Aso;

    public AsoAlertaProvider(IAppDbContext db) => _db = db;

    public async Task<List<AlertaOrigemItem>> ObterItensAsync(CancellationToken ct = default)
    {
        var asos = await _db.Asos
            .Include(a => a.Trabalhador)
            .ToListAsync(ct);

        // O ASO vigente de cada trabalhador é o de validade mais distante; os anteriores foram renovados
        // e não podem manter alerta de vencido aberto.
        var vigentePorTrabalhador = asos
            .GroupBy(a => a.TrabalhadorId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.DataValidade).ThenByDescending(a => a.DataExame).First().Id);

        return asos.Select(aso => new AlertaOrigemItem
        {
            Substituido = vigentePorTrabalhador[aso.TrabalhadorId] != aso.Id,
            EntidadeOrigemTipo = "Aso",
            EntidadeOrigemId = aso.Id,
            DataVencimento = aso.DataValidade,
            TipoAlertaVencendo = TipoAlerta.AsoVencendo,
            TipoAlertaVencido = TipoAlerta.AsoVencido,
            Titulo = $"ASO de {aso.Trabalhador?.Nome ?? "trabalhador"} — validade {aso.DataValidade:dd/MM/yyyy}",
            TrabalhadorId = aso.TrabalhadorId,
            ObraId = aso.Trabalhador?.ObraId,
        }).ToList();
    }
}

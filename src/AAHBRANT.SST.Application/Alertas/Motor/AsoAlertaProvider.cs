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

        return asos.Select(aso => new AlertaOrigemItem
        {
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

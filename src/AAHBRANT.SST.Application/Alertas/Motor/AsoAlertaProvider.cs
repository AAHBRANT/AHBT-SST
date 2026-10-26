using AAHBRANT.SST.Application.Asos;
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

        // O ASO vigente de cada trabalhador é o do exame mais recente (AsoVigente, regra única); os anteriores
        // foram renovados e não podem manter alerta de vencido aberto.
        var vigentePorTrabalhador = AsoVigente.PorTrabalhador(
                asos.Select(a => new AsoResumo(a.Id, a.TrabalhadorId, a.DataExame, a.DataValidade, a.ResultadoStatus)))
            .ToDictionary(kv => kv.Key, kv => kv.Value.Id);

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

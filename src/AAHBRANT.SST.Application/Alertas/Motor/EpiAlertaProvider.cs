using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alertas.Motor;

public class EpiAlertaProvider : IAlertaOrigemProvider
{
    private readonly IAppDbContext _db;

    public TipoModuloAlerta Modulo => TipoModuloAlerta.Epi;

    public EpiAlertaProvider(IAppDbContext db) => _db = db;

    public async Task<List<AlertaOrigemItem>> ObterItensAsync(CancellationToken ct = default)
    {
        // Só entregas com DataValidade preenchida e ainda não devolvidas entram no motor — uma
        // entrega já devolvida (DataDevolucao preenchida) não precisa mais de alerta de vencimento.
        var entregas = await _db.EntregasEpi
            .Include(e => e.Trabalhador)
            .Include(e => e.CatalogoEpi)
            .Where(e => e.DataValidade != null && e.DataDevolucao == null)
            .ToListAsync(ct);

        return entregas.Select(entrega => new AlertaOrigemItem
        {
            EntidadeOrigemTipo = "EntregaEpi",
            EntidadeOrigemId = entrega.Id,
            DataVencimento = entrega.DataValidade!.Value,
            TipoAlertaVencendo = TipoAlerta.EpiValidadeProxima,
            TipoAlertaVencido = TipoAlerta.EpiVencido,
            Titulo = $"{entrega.CatalogoEpi?.Nome ?? "EPI"} de {entrega.Trabalhador?.Nome ?? "trabalhador"} — validade {entrega.DataValidade:dd/MM/yyyy}",
            TrabalhadorId = entrega.TrabalhadorId,
            ObraId = entrega.Trabalhador?.ObraId,
        }).ToList();
    }
}

using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alertas.Motor;

public class TreinamentoAlertaProvider : IAlertaOrigemProvider
{
    private readonly IAppDbContext _db;

    public TipoModuloAlerta Modulo => TipoModuloAlerta.Treinamento;

    public TreinamentoAlertaProvider(IAppDbContext db) => _db = db;

    public async Task<List<AlertaOrigemItem>> ObterItensAsync(CancellationToken ct = default)
    {
        var treinamentos = await _db.Treinamentos
            .Include(t => t.Trabalhador)
            .Include(t => t.CursoTreinamento)
            .ToListAsync(ct);

        return treinamentos.Select(t => new AlertaOrigemItem
        {
            EntidadeOrigemTipo = "Treinamento",
            EntidadeOrigemId = t.Id,
            DataVencimento = t.DataValidade,
            TipoAlertaVencendo = TipoAlerta.TreinamentoVencendo,
            TipoAlertaVencido = TipoAlerta.TreinamentoVencido,
            Titulo = $"{t.CursoTreinamento?.Nome ?? "Treinamento"} de {t.Trabalhador?.Nome ?? "trabalhador"} — validade {t.DataValidade:dd/MM/yyyy}",
            TrabalhadorId = t.TrabalhadorId,
            ObraId = t.Trabalhador?.ObraId,
        }).ToList();
    }
}

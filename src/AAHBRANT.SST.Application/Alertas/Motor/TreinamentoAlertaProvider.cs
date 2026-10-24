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

        // O treinamento vigente de cada trabalhador em cada curso é o de validade mais distante; os
        // anteriores foram renovados e não podem manter alerta de vencido aberto.
        var vigentePorCurso = treinamentos
            .GroupBy(t => (t.TrabalhadorId, t.CursoTreinamentoId))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.DataValidade).ThenByDescending(t => t.DataRealizacao).First().Id);

        return treinamentos.Select(t => new AlertaOrigemItem
        {
            Substituido = vigentePorCurso[(t.TrabalhadorId, t.CursoTreinamentoId)] != t.Id,
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

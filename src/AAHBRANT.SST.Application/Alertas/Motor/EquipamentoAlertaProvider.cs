using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alertas.Motor;

public class EquipamentoAlertaProvider : IAlertaOrigemProvider
{
    private readonly IAppDbContext _db;

    public TipoModuloAlerta Modulo => TipoModuloAlerta.Equipamento;

    public EquipamentoAlertaProvider(IAppDbContext db) => _db = db;

    public async Task<List<AlertaOrigemItem>> ObterItensAsync(CancellationToken ct = default)
    {
        var ativos = await _db.AtivosSst
            .Include(a => a.Obra)
            .Where(a => a.TipoAtivo == TipoAtivo.Equipamento)
            .ToListAsync(ct);

        return ativos.Select(ativo => new AlertaOrigemItem
        {
            EntidadeOrigemTipo = "AtivoSst",
            EntidadeOrigemId = ativo.Id,
            DataVencimento = ativo.DataValidade,
            TipoAlertaVencendo = TipoAlerta.EquipamentoVencendo,
            TipoAlertaVencido = TipoAlerta.EquipamentoVencido,
            Titulo = $"Equipamento {ativo.Identificacao} — {ativo.Descricao} — validade {ativo.DataValidade:dd/MM/yyyy}",
            ObraId = ativo.ObraId,
        }).ToList();
    }
}

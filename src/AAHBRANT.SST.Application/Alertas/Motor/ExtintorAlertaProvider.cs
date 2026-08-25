using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alertas.Motor;

public class ExtintorAlertaProvider : IAlertaOrigemProvider
{
    private readonly IAppDbContext _db;

    public TipoModuloAlerta Modulo => TipoModuloAlerta.Extintor;

    public ExtintorAlertaProvider(IAppDbContext db) => _db = db;

    public async Task<List<AlertaOrigemItem>> ObterItensAsync(CancellationToken ct = default)
    {
        var ativos = await _db.AtivosSst
            .Include(a => a.Obra)
            .Where(a => a.TipoAtivo == TipoAtivo.Extintor)
            .ToListAsync(ct);

        return ativos.Select(ativo => new AlertaOrigemItem
        {
            EntidadeOrigemTipo = "AtivoSst",
            EntidadeOrigemId = ativo.Id,
            DataVencimento = ativo.DataValidade,
            TipoAlertaVencendo = TipoAlerta.ExtintorVencendo,
            TipoAlertaVencido = TipoAlerta.ExtintorVencido,
            Titulo = $"Extintor {ativo.Identificacao} — {ativo.Descricao} — validade {ativo.DataValidade:dd/MM/yyyy}",
            ObraId = ativo.ObraId,
        }).ToList();
    }
}

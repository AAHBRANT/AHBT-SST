using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alertas.Motor;

// "Vigência do Programa" do PGR (Início / Revisão Sugerida / Término) — pedido do usuário, 02/09:
// duas datas de controle distintas, cada uma com seu próprio alerta. Um PGR gera até 2 itens aqui
// (EntidadeOrigemTipo diferente por item, mesmo Pgr.Id) para que o motor trate Término e Revisão
// Sugerida como alertas independentes, cada um podendo estar aberto/resolvido no seu próprio ritmo.
// PGRs sem DataTermino (cadastrados antes deste campo existir) ou já Encerrados não geram alerta.
public class PgrAlertaProvider : IAlertaOrigemProvider
{
    private readonly IAppDbContext _db;

    public TipoModuloAlerta Modulo => TipoModuloAlerta.Pgr;

    public PgrAlertaProvider(IAppDbContext db) => _db = db;

    public async Task<List<AlertaOrigemItem>> ObterItensAsync(CancellationToken ct = default)
    {
        var pgrs = await _db.Pgrs
            .Include(p => p.Obra)
            .Where(p => p.Status != StatusPgr.Encerrado)
            .ToListAsync(ct);

        var itens = new List<AlertaOrigemItem>();

        foreach (var pgr in pgrs)
        {
            if (pgr.DataTermino.HasValue)
            {
                itens.Add(new AlertaOrigemItem
                {
                    EntidadeOrigemTipo = "PgrTermino",
                    EntidadeOrigemId = pgr.Id,
                    DataVencimento = pgr.DataTermino.Value,
                    TipoAlertaVencendo = TipoAlerta.PgrVencendo,
                    TipoAlertaVencido = TipoAlerta.PgrVencido,
                    Titulo = $"PGR '{pgr.Nome}' — término da vigência {pgr.DataTermino.Value:dd/MM/yyyy}",
                    ObraId = pgr.ObraId,
                });
            }

            if (pgr.DataProximaRevisao.HasValue)
            {
                itens.Add(new AlertaOrigemItem
                {
                    EntidadeOrigemTipo = "PgrRevisao",
                    EntidadeOrigemId = pgr.Id,
                    DataVencimento = pgr.DataProximaRevisao.Value,
                    TipoAlertaVencendo = TipoAlerta.PgrRevisaoVencendo,
                    TipoAlertaVencido = TipoAlerta.PgrRevisaoVencida,
                    Titulo = $"Revisão sugerida do PGR '{pgr.Nome}' — {pgr.DataProximaRevisao.Value:dd/MM/yyyy}",
                    ObraId = pgr.ObraId,
                });
            }
        }

        return itens;
    }
}

using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alertas.Motor;

// Validade do PCMSO (pedido do usuário, 16/10/2026: "PGR e PCMSO também"). Mesmo desenho do
// PgrAlertaProvider: um item por documento com Validade preenchida. Rascunho ainda não vale como
// programa da obra, e Obsoleto/Cancelado já foi substituído — nenhum dos três gera alerta.
public class PcmsoAlertaProvider : IAlertaOrigemProvider
{
    public const string EntidadeOrigem = "PcmsoValidade";

    private readonly IAppDbContext _db;

    public TipoModuloAlerta Modulo => TipoModuloAlerta.Pcmso;

    public PcmsoAlertaProvider(IAppDbContext db) => _db = db;

    public async Task<List<AlertaOrigemItem>> ObterItensAsync(CancellationToken ct = default)
    {
        var pcmsos = await _db.PcmsoDetalhes
            .Where(p => p.Validade != null)
            .Where(p => p.Status != StatusPcmsoDocumento.Rascunho
                && p.Status != StatusPcmsoDocumento.Obsoleto
                && p.Status != StatusPcmsoDocumento.Cancelado)
            .Select(p => new { p.Id, p.Nome, p.Validade, p.ObraId })
            .ToListAsync(ct);

        return pcmsos.Select(p => new AlertaOrigemItem
        {
            EntidadeOrigemTipo = EntidadeOrigem,
            EntidadeOrigemId = p.Id,
            DataVencimento = p.Validade!.Value,
            TipoAlertaVencendo = TipoAlerta.PcmsoVencendo,
            TipoAlertaVencido = TipoAlerta.PcmsoVencido,
            Titulo = $"PCMSO '{p.Nome}' — validade {p.Validade.Value:dd/MM/yyyy}",
            ObraId = p.ObraId,
        }).ToList();
    }
}

using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alertas.Motor;

// Cobre todo DocumentoGestao com Validade preenchida — inclui PCMSO (Tipo="PCMSO", PR-SST-003),
// já que Pcmso reaproveita DocumentoGestao sem entidade de vencimento própria.
public class DocumentoAlertaProvider : IAlertaOrigemProvider
{
    private readonly IAppDbContext _db;

    public TipoModuloAlerta Modulo => TipoModuloAlerta.Documento;

    public DocumentoAlertaProvider(IAppDbContext db) => _db = db;

    public async Task<List<AlertaOrigemItem>> ObterItensAsync(CancellationToken ct = default)
    {
        var documentos = await _db.DocumentosGestao
            .Where(d => d.Validade.HasValue)
            .ToListAsync(ct);

        return documentos.Select(d => new AlertaOrigemItem
        {
            EntidadeOrigemTipo = "DocumentoGestao",
            EntidadeOrigemId = d.Id,
            DataVencimento = d.Validade!.Value,
            TipoAlertaVencendo = TipoAlerta.DocumentoVencendo,
            TipoAlertaVencido = TipoAlerta.DocumentoVencido,
            Titulo = $"{d.Nome} — validade {d.Validade:dd/MM/yyyy}",
            ObraId = d.ObraId,
        }).ToList();
    }
}

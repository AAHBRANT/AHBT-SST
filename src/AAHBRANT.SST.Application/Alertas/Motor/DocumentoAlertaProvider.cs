using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alertas.Motor;

// Cobria todo DocumentoGestao com Validade preenchida — DocumentoGestao foi removido junto com
// Gestão Documental/Conformidade em 2026-08-28. Reformulado em 2026-09-03 (junto com a reformulação
// de PcmsoDetalhe, ver nota em Domain/Entidades/SaudeOcupacional/SaudeOcupacional.cs) para cobrir só
// PCMSO — o único "documento controlado" que sobrou depois da remoção do módulo — em vez de todo
// DocumentoGestao genérico. Documentos Obsoletos/Cancelados não geram alerta (já não estão vigentes,
// não há ação de renovação pendente).
public class DocumentoAlertaProvider : IAlertaOrigemProvider
{
    private readonly IAppDbContext _db;

    public TipoModuloAlerta Modulo => TipoModuloAlerta.Documento;

    public DocumentoAlertaProvider(IAppDbContext db) => _db = db;

    public async Task<List<AlertaOrigemItem>> ObterItensAsync(CancellationToken ct = default)
    {
        var pcmsos = await _db.PcmsoDetalhes
            .Where(p => p.Validade.HasValue
                && p.Status != StatusPcmsoDocumento.Obsoleto
                && p.Status != StatusPcmsoDocumento.Cancelado)
            .ToListAsync(ct);

        return pcmsos.Select(p => new AlertaOrigemItem
        {
            EntidadeOrigemTipo = "Pcmso",
            EntidadeOrigemId = p.Id,
            DataVencimento = p.Validade!.Value,
            TipoAlertaVencendo = TipoAlerta.DocumentoVencendo,
            TipoAlertaVencido = TipoAlerta.DocumentoVencido,
            Titulo = $"PCMSO '{p.Nome}' — validade {p.Validade.Value:dd/MM/yyyy}",
            ObraId = p.ObraId,
        }).ToList();
    }
}

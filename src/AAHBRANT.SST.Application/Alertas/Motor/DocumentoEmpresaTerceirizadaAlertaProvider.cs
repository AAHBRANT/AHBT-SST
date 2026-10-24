using System.Security.Cryptography;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alertas.Motor;

// Validade dos documentos da empresa terceirizada (PGR, PCMSO, cartão CNPJ...), especificação 21/10/2026:
// quando um documento vence, o item reabre no checklist da empresa e os técnicos são avisados. A empresa
// não tem obra própria — trabalha nas obras dos seus contratos vigentes —, então sai um alerta por obra,
// para cada técnico ver o da sua obra. Documento exigido desativado não gera alerta.
public class DocumentoEmpresaTerceirizadaAlertaProvider : IAlertaOrigemProvider
{
    public const string TipoOrigem = "DocumentoEmpresaTerceirizada";

    private readonly IAppDbContext _db;

    public TipoModuloAlerta Modulo => TipoModuloAlerta.DocumentoTerceirizado;

    public DocumentoEmpresaTerceirizadaAlertaProvider(IAppDbContext db) => _db = db;

    public async Task<List<AlertaOrigemItem>> ObterItensAsync(CancellationToken ct = default)
    {
        var documentos = await _db.DocumentosEmpresaTerceirizada
            .Where(d => d.DataValidade != null && d.DocumentoExigido!.Ativo)
            .Select(d => new
            {
                d.Id,
                d.EmpresaId,
                DataValidade = d.DataValidade!.Value,
                Documento = d.DocumentoExigido!.Nome,
                Empresa = d.Empresa!.RazaoSocial,
            })
            .ToListAsync(ct);
        if (documentos.Count == 0) return new List<AlertaOrigemItem>();

        var empresaIds = documentos.Select(d => d.EmpresaId).Distinct().ToList();
        var obrasPorEmpresa = (await _db.Contratos
                .Where(c => empresaIds.Contains(c.EmpresaId) && c.Status == StatusContrato.Validado)
                .Select(c => new { c.EmpresaId, c.ObraId })
                .Distinct()
                .ToListAsync(ct))
            .GroupBy(c => c.EmpresaId)
            .ToDictionary(g => g.Key, g => g.Select(c => c.ObraId).ToList());

        var itens = new List<AlertaOrigemItem>();
        foreach (var d in documentos)
        {
            // Sem contrato vigente a empresa não está em obra nenhuma: nada a avisar.
            if (!obrasPorEmpresa.TryGetValue(d.EmpresaId, out var obras)) continue;
            foreach (var obraId in obras)
                itens.Add(new AlertaOrigemItem
                {
                    EntidadeOrigemTipo = TipoOrigem,
                    EntidadeOrigemId = IdPorObra(d.Id, obraId),
                    DataVencimento = d.DataValidade.ToDateTime(TimeOnly.MinValue),
                    TipoAlertaVencendo = TipoAlerta.DocumentoTerceirizadoVencendo,
                    TipoAlertaVencido = TipoAlerta.DocumentoTerceirizadoVencido,
                    Titulo = $"{d.Documento} da {d.Empresa} — validade {d.DataValidade:dd/MM/yyyy}",
                    Descricao = "Peça o documento atualizado à empresa e envie no checklist da empresa (Terceirizados).",
                    ObraId = obraId,
                });
        }
        return itens;
    }

    // O motor deduplica por (tipo, id): o mesmo documento em duas obras precisa de dois ids estáveis.
    public static Guid IdPorObra(Guid documentoId, Guid obraId)
    {
        var bytes = new byte[32];
        documentoId.TryWriteBytes(bytes.AsSpan(0, 16));
        obraId.TryWriteBytes(bytes.AsSpan(16, 16));
        return new Guid(MD5.HashData(bytes));
    }
}

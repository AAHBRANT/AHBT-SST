using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura.Queries;

// Painel administrativo (docs/Motor-Assinatura-Eletronica.md §5, etapa 12) — visão geral de todos os
// documentos do motor, cruzando módulos (Dds hoje, Treinamento/EPI/APR/PT/Inspeções depois). Ao
// contrário de DocumentoPublicoDto (página pública), aqui o consumidor já é autenticado e autorizado
// via "assinatura:ver" (mesma policy do endpoint de detalhe/PDF), então Id/EntidadeId podem ser
// expostos — são necessários para os botões de ação (baixar PDF, copiar link público).
public record DocumentoAssinaturaResumoDto(
    Guid Id,
    string EntidadeTipo,
    Guid EntidadeId,
    StatusDocumentoAssinatura Status,
    DateTime CriadoEm,
    DateTime? FinalizadoEm,
    int QuantidadeSignatarios,
    bool TemPdf,
    string? TokenValidacaoPublica);

public record ListarDocumentosAssinaturaQuery(
    string? EntidadeTipo = null,
    StatusDocumentoAssinatura? Status = null,
    DateTime? DataInicio = null,
    DateTime? DataFim = null) : IRequest<List<DocumentoAssinaturaResumoDto>>;

public class ListarDocumentosAssinaturaQueryHandler
    : IRequestHandler<ListarDocumentosAssinaturaQuery, List<DocumentoAssinaturaResumoDto>>
{
    private readonly IAppDbContext _db;

    public ListarDocumentosAssinaturaQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<DocumentoAssinaturaResumoDto>> Handle(ListarDocumentosAssinaturaQuery request, CancellationToken ct)
    {
        var query = _db.DocumentosAssinatura.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.EntidadeTipo))
            query = query.Where(d => d.EntidadeTipo == request.EntidadeTipo);
        if (request.Status.HasValue)
            query = query.Where(d => d.Status == request.Status.Value);
        // Filtro de data usa CreatedAtUtc (não FinalizadoEm) porque documentos EmAndamento/Cancelado
        // têm FinalizadoEm nulo e ainda assim precisam aparecer num filtro de período.
        if (request.DataInicio.HasValue)
            query = query.Where(d => d.CreatedAtUtc >= request.DataInicio.Value);
        if (request.DataFim.HasValue)
            query = query.Where(d => d.CreatedAtUtc <= request.DataFim.Value);

        return await query
            .OrderByDescending(d => d.CreatedAtUtc)
            .Select(d => new DocumentoAssinaturaResumoDto(
                d.Id, d.EntidadeTipo, d.EntidadeId, d.Status, d.CreatedAtUtc, d.FinalizadoEm,
                d.Signatarios.Count, d.PdfConteudo != null, d.TokenValidacaoPublica))
            .ToListAsync(ct);
    }
}

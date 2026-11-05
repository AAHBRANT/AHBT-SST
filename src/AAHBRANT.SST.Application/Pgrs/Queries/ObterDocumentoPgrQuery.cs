using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.GeracaoPgr;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Pgrs.Queries;

public class DocumentoPgrResultado
{
    public byte[] Conteudo { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = string.Empty;
    public string NomeArquivo { get; set; } = string.Empty;
}

public record ObterDocumentoPgrQuery(Guid PgrId) : IRequest<DocumentoPgrResultado?>;

public class ObterDocumentoPgrQueryHandler : IRequestHandler<ObterDocumentoPgrQuery, DocumentoPgrResultado?>
{
    private readonly IAppDbContext _db;
    private readonly IMediator? _mediator;

    public ObterDocumentoPgrQueryHandler(IAppDbContext db, IMediator? mediator = null)
    {
        _db = db;
        _mediator = mediator;
    }

    public async Task<DocumentoPgrResultado?> Handle(ObterDocumentoPgrQuery request, CancellationToken ct)
    {
        var pgr = await _db.Pgrs.FirstOrDefaultAsync(p => p.Id == request.PgrId, ct);
        if (pgr is null) return null;

        if (pgr.DocumentoConteudo is not null && pgr.DocumentoContentType is not null)
            return new DocumentoPgrResultado
            {
                Conteudo = pgr.DocumentoConteudo,
                ContentType = pgr.DocumentoContentType,
                NomeArquivo = $"pgr-{pgr.Id}.pdf",
            };

        // PGR aprovado pelo gerador da EAP (05/11/2026): a aprovação guarda o PDF anterior em Revisões e não anexa
        // arquivo ao PGR, então a aba ficava "Nenhum documento anexado". Mostra o PDF da última geração aprovada:
        // o assinado e congelado, quando existe (o mesmo do download do gerador), senão o montado na hora.
        var geracao = await _db.GeracoesPgrEap
            .Where(g => g.PgrId == pgr.Id && g.Status == StatusGeracaoPgrEap.Aprovado)
            .OrderByDescending(g => g.AprovadoEmUtc)
            .Select(g => new { g.Id })
            .FirstOrDefaultAsync(ct);
        if (geracao is null) return null;

        var assinado = await _db.DocumentosAssinatura
            .Where(d => d.EntidadeTipo == AssinaturaGeracaoPgr.EntidadeTipo && d.EntidadeId == geracao.Id
                && d.Status == StatusDocumentoAssinatura.Finalizado && d.PdfConteudo != null)
            .Select(d => d.PdfConteudo)
            .FirstOrDefaultAsync(ct);
        var conteudo = assinado is { Length: > 0 }
            ? assinado
            : _mediator is null ? null : (await _mediator.Send(new GerarPdfGeracaoPgrEapQuery(geracao.Id), ct)).Conteudo;
        if (conteudo is null) return null;

        return new DocumentoPgrResultado
        {
            Conteudo = conteudo,
            ContentType = "application/pdf",
            NomeArquivo = $"pgr-{pgr.Id}.pdf",
        };
    }
}

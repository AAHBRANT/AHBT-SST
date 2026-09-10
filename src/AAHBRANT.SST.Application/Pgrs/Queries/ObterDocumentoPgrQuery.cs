using AAHBRANT.SST.Application.Common.Interfaces;
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

    public ObterDocumentoPgrQueryHandler(IAppDbContext db) => _db = db;

    public async Task<DocumentoPgrResultado?> Handle(ObterDocumentoPgrQuery request, CancellationToken ct)
    {
        var pgr = await _db.Pgrs.FirstOrDefaultAsync(p => p.Id == request.PgrId, ct);
        if (pgr?.DocumentoConteudo is null || pgr.DocumentoContentType is null) return null;

        return new DocumentoPgrResultado
        {
            Conteudo = pgr.DocumentoConteudo,
            ContentType = pgr.DocumentoContentType,
            NomeArquivo = $"pgr-{pgr.Id}.pdf",
        };
    }
}

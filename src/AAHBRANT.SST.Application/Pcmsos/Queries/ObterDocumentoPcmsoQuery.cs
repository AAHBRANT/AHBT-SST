using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Pcmsos.Queries;

public class DocumentoPcmsoResultado
{
    public byte[] Conteudo { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = string.Empty;
    public string NomeArquivo { get; set; } = string.Empty;
}

public record ObterDocumentoPcmsoQuery(Guid PcmsoId) : IRequest<DocumentoPcmsoResultado?>;

public class ObterDocumentoPcmsoQueryHandler : IRequestHandler<ObterDocumentoPcmsoQuery, DocumentoPcmsoResultado?>
{
    private readonly IAppDbContext _db;

    public ObterDocumentoPcmsoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<DocumentoPcmsoResultado?> Handle(ObterDocumentoPcmsoQuery request, CancellationToken ct)
    {
        var pcmso = await _db.PcmsoDetalhes.FirstOrDefaultAsync(p => p.Id == request.PcmsoId, ct);
        if (pcmso?.DocumentoConteudo is null || pcmso.DocumentoContentType is null) return null;

        return new DocumentoPcmsoResultado
        {
            Conteudo = pcmso.DocumentoConteudo,
            ContentType = pcmso.DocumentoContentType,
            NomeArquivo = $"pcmso-{pcmso.Id}.pdf",
        };
    }
}

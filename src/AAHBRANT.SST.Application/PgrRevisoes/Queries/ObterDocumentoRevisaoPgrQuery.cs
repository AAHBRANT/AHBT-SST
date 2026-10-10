using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Pgrs.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PgrRevisoes.Queries;

// PDF de uma revisão do PGR. A obra é conferida pelo PGR (Pgrs tem filtro por obra; PgrRevisoes não).
public record ObterDocumentoRevisaoPgrQuery(Guid RevisaoId) : IRequest<DocumentoPgrResultado?>;

public class ObterDocumentoRevisaoPgrQueryHandler : IRequestHandler<ObterDocumentoRevisaoPgrQuery, DocumentoPgrResultado?>
{
    private readonly IAppDbContext _db;

    public ObterDocumentoRevisaoPgrQueryHandler(IAppDbContext db) => _db = db;

    public async Task<DocumentoPgrResultado?> Handle(ObterDocumentoRevisaoPgrQuery request, CancellationToken ct)
    {
        var pgrsVisiveis = _db.Pgrs.Select(p => p.Id);
        var revisao = await _db.PgrRevisoes
            .Where(r => r.Id == request.RevisaoId && pgrsVisiveis.Contains(r.PgrId))
            .Select(r => new { r.NumeroRevisao, r.DocumentoConteudo, r.DocumentoContentType, r.DocumentoNomeArquivo })
            .FirstOrDefaultAsync(ct);
        if (revisao?.DocumentoConteudo is null) return null;

        return new DocumentoPgrResultado
        {
            Conteudo = revisao.DocumentoConteudo,
            ContentType = revisao.DocumentoContentType ?? "application/pdf",
            NomeArquivo = revisao.DocumentoNomeArquivo ?? $"pgr-revisao-{revisao.NumeroRevisao:00}.pdf",
        };
    }
}

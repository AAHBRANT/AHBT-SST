using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura.Queries;

// Bytes crus do PDF, à parte de DocumentoAssinaturaDto — não faz sentido carregar/serializar o PDF
// inteiro toda vez que o quiosque consulta status/signatários via ObterDocumentoQuery (polling
// frequente); esta query só é chamada quando o usuário efetivamente pede o download.
public record ObterPdfDocumentoQuery(Guid DocumentoAssinaturaId) : IRequest<byte[]?>;

public class ObterPdfDocumentoQueryValidator : AbstractValidator<ObterPdfDocumentoQuery>
{
    public ObterPdfDocumentoQueryValidator()
    {
        RuleFor(x => x.DocumentoAssinaturaId).NotEmpty();
    }
}

public class ObterPdfDocumentoQueryHandler : IRequestHandler<ObterPdfDocumentoQuery, byte[]?>
{
    private readonly IAppDbContext _db;

    public ObterPdfDocumentoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<byte[]?> Handle(ObterPdfDocumentoQuery request, CancellationToken ct)
    {
        var origem = await _db.DocumentosAssinatura
            .Where(d => d.Id == request.DocumentoAssinaturaId)
            .Select(d => new { d.EntidadeTipo, d.EntidadeId })
            .FirstOrDefaultAsync(ct);
        if (origem is null || !await EscopoDocumentoAssinatura.PodeVerAsync(_db, origem.EntidadeTipo, origem.EntidadeId, ct))
            return null;

        return await _db.DocumentosAssinatura
            .Where(d => d.Id == request.DocumentoAssinaturaId)
            .Select(d => d.PdfConteudo)
            .FirstOrDefaultAsync(ct);
    }
}

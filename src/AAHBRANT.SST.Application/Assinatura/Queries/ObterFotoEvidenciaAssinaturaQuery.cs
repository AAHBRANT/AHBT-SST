using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura.Queries;

public record FotoEvidenciaAssinaturaResultado(byte[] Conteudo, string ContentType, string NomeArquivo);

public record ObterFotoEvidenciaAssinaturaQuery(Guid SignatarioId) : IRequest<FotoEvidenciaAssinaturaResultado?>;

public class ObterFotoEvidenciaAssinaturaQueryHandler : IRequestHandler<ObterFotoEvidenciaAssinaturaQuery, FotoEvidenciaAssinaturaResultado?>
{
    private readonly IAppDbContext _db;

    public ObterFotoEvidenciaAssinaturaQueryHandler(IAppDbContext db) => _db = db;

    public async Task<FotoEvidenciaAssinaturaResultado?> Handle(ObterFotoEvidenciaAssinaturaQuery request, CancellationToken ct)
    {
        var foto = await _db.DocumentoSignatarios
            .Where(s => s.Id == request.SignatarioId && s.FotoEvidenciaConteudo != null)
            .Select(s => new
            {
                Conteudo = s.FotoEvidenciaConteudo!,
                ContentType = s.FotoEvidenciaContentType ?? "image/jpeg",
            })
            .FirstOrDefaultAsync(ct);

        return foto is null
            ? null
            : new FotoEvidenciaAssinaturaResultado(
                foto.Conteudo,
                foto.ContentType,
                $"evidencia-assinatura-{request.SignatarioId}.jpg");
    }
}

using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura.Queries;

public record FotoEvidenciaAssinaturaResultado(byte[] Conteudo, string ContentType, string NomeArquivo);

public record ObterFotoEvidenciaAssinaturaQuery(Guid SignatarioId) : IRequest<FotoEvidenciaAssinaturaResultado?>;

public class ObterFotoEvidenciaAssinaturaQueryHandler : IRequestHandler<ObterFotoEvidenciaAssinaturaQuery, FotoEvidenciaAssinaturaResultado?>
{
    private readonly IAppDbContext _db;
    private readonly ICertificadoAssinaturasPdfService? _certificadoPdf;

    public ObterFotoEvidenciaAssinaturaQueryHandler(IAppDbContext db, ICertificadoAssinaturasPdfService? certificadoPdf = null)
    {
        _db = db;
        _certificadoPdf = certificadoPdf;
    }

    public async Task<FotoEvidenciaAssinaturaResultado?> Handle(ObterFotoEvidenciaAssinaturaQuery request, CancellationToken ct)
    {
        // Foto facial é dado biométrico: só sai para quem é da obra do próprio signatário, e sempre
        // estampada com a marca d'água "CONFIDENCIAL" + data/hora da assinatura (LGPD).
        var foto = await _db.DocumentoSignatarios
            .Where(s => s.Id == request.SignatarioId && s.FotoEvidenciaConteudo != null)
            .NoEscopoDoTrabalhador(_db, s => s.TrabalhadorId)
            .Select(s => new
            {
                Conteudo = s.FotoEvidenciaConteudo!,
                ContentType = s.FotoEvidenciaContentType ?? "image/jpeg",
                s.AssinadoEm,
                s.MetodoAutenticacao,
            })
            .FirstOrDefaultAsync(ct);

        if (foto is null) return null;

        var retrato34 = foto.MetodoAutenticacao != MetodoAutenticacaoAssinatura.Biometria;
        var conteudo = _certificadoPdf is not null
            ? _certificadoPdf.EstamparFotoConfidencial(foto.Conteudo, foto.AssinadoEm, retrato34)
            : foto.Conteudo;

        return new FotoEvidenciaAssinaturaResultado(
            conteudo,
            "image/jpeg",
            $"evidencia-assinatura-{request.SignatarioId}.jpg");
    }
}

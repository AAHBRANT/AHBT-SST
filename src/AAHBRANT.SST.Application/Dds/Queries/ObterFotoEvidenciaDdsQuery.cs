using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Queries;

public record ObterFotoEvidenciaDdsQuery(Guid FotoId) : IRequest<FotoEvidenciaDdsResultado?>;

public class FotoEvidenciaDdsResultado
{
    public byte[] Conteudo { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = string.Empty;
    public string NomeArquivo { get; set; } = string.Empty;
}

public class ObterFotoEvidenciaDdsQueryHandler : IRequestHandler<ObterFotoEvidenciaDdsQuery, FotoEvidenciaDdsResultado?>
{
    private readonly IAppDbContext _db;

    public ObterFotoEvidenciaDdsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<FotoEvidenciaDdsResultado?> Handle(ObterFotoEvidenciaDdsQuery request, CancellationToken ct)
    {
        var foto = await _db.DdsFotosEvidencia.FirstOrDefaultAsync(f => f.Id == request.FotoId, ct);
        if (foto is null) return null;

        var extensao = foto.FotoContentType == "image/png" ? "png" : "jpg";
        return new FotoEvidenciaDdsResultado
        {
            Conteudo = foto.FotoConteudo,
            ContentType = string.IsNullOrEmpty(foto.FotoContentType) ? "application/octet-stream" : foto.FotoContentType,
            NomeArquivo = $"dds-evidencia-{foto.DdsId}-{foto.Ordem}.{extensao}",
        };
    }
}

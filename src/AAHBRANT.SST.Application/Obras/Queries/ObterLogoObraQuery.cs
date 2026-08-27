using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Obras.Queries;

public record ObterLogoObraQuery(Guid ObraId) : IRequest<LogoObraResultado?>;

public class LogoObraResultado
{
    public byte[] Conteudo { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = string.Empty;
    public string NomeArquivo { get; set; } = string.Empty;
}

public class ObterLogoObraQueryHandler : IRequestHandler<ObterLogoObraQuery, LogoObraResultado?>
{
    private readonly IAppDbContext _db;

    public ObterLogoObraQueryHandler(IAppDbContext db) => _db = db;

    public async Task<LogoObraResultado?> Handle(ObterLogoObraQuery request, CancellationToken ct)
    {
        var obra = await _db.Obras
            .FirstOrDefaultAsync(o => o.Id == request.ObraId, ct);

        if (obra is null || obra.LogoConteudo is null || obra.LogoConteudo.Length == 0) return null;

        var extensao = obra.LogoContentType == "image/png" ? "png" : "jpg";
        return new LogoObraResultado
        {
            Conteudo = obra.LogoConteudo,
            ContentType = string.IsNullOrEmpty(obra.LogoContentType) ? "application/octet-stream" : obra.LogoContentType,
            NomeArquivo = $"obra-{obra.Id}-logo.{extensao}",
        };
    }
}

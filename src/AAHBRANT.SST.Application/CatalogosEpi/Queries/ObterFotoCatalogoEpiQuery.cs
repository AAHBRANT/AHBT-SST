using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CatalogosEpi.Queries;

// Mesmo padrão de ObterFotoTrabalhadorQuery/ObterLogoObraQuery — serve o binário da foto por
// endpoint dedicado, nunca embutido no CatalogoEpiDto de listagem (só o flag TemFoto).
public record ObterFotoCatalogoEpiQuery(Guid CatalogoEpiId) : IRequest<FotoCatalogoEpiResultado?>;

public class FotoCatalogoEpiResultado
{
    public byte[] Conteudo { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = string.Empty;
    public string NomeArquivo { get; set; } = string.Empty;
}

public class ObterFotoCatalogoEpiQueryHandler : IRequestHandler<ObterFotoCatalogoEpiQuery, FotoCatalogoEpiResultado?>
{
    private readonly IAppDbContext _db;

    public ObterFotoCatalogoEpiQueryHandler(IAppDbContext db) => _db = db;

    public async Task<FotoCatalogoEpiResultado?> Handle(ObterFotoCatalogoEpiQuery request, CancellationToken ct)
    {
        var epi = await _db.CatalogoEpis.FirstOrDefaultAsync(x => x.Id == request.CatalogoEpiId, ct);

        if (epi is null || epi.FotoConteudo is null || epi.FotoConteudo.Length == 0) return null;

        var extensao = epi.FotoContentType == "image/png" ? "png" : "jpg";
        return new FotoCatalogoEpiResultado
        {
            Conteudo = epi.FotoConteudo,
            ContentType = string.IsNullOrEmpty(epi.FotoContentType) ? "application/octet-stream" : epi.FotoContentType,
            NomeArquivo = $"epi-{epi.Id}-foto.{extensao}",
        };
    }
}

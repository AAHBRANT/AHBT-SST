using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CatalogosUniforme.Queries;

// Mesmo padrão de ObterFotoCatalogoEpiQuery — serve o binário da foto por endpoint dedicado, nunca
// embutido no CatalogoUniformeDto de listagem (só o flag TemFoto).
public record ObterFotoCatalogoUniformeQuery(Guid CatalogoUniformeId) : IRequest<FotoCatalogoUniformeResultado?>;

public class FotoCatalogoUniformeResultado
{
    public byte[] Conteudo { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = string.Empty;
    public string NomeArquivo { get; set; } = string.Empty;
}

public class ObterFotoCatalogoUniformeQueryHandler : IRequestHandler<ObterFotoCatalogoUniformeQuery, FotoCatalogoUniformeResultado?>
{
    private readonly IAppDbContext _db;

    public ObterFotoCatalogoUniformeQueryHandler(IAppDbContext db) => _db = db;

    public async Task<FotoCatalogoUniformeResultado?> Handle(ObterFotoCatalogoUniformeQuery request, CancellationToken ct)
    {
        var item = await _db.CatalogoUniformes.FirstOrDefaultAsync(x => x.Id == request.CatalogoUniformeId, ct);

        if (item is null || item.FotoConteudo is null || item.FotoConteudo.Length == 0) return null;

        var extensao = item.FotoContentType == "image/png" ? "png" : "jpg";
        return new FotoCatalogoUniformeResultado
        {
            Conteudo = item.FotoConteudo,
            ContentType = string.IsNullOrEmpty(item.FotoContentType) ? "application/octet-stream" : item.FotoContentType,
            NomeArquivo = $"uniforme-{item.Id}-foto.{extensao}",
        };
    }
}

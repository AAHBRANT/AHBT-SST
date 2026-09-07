using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CatalogosEpc.Queries;

// Mesmo padrão de ObterFotoCatalogoEpiQuery/ObterFotoCatalogoUniformeQuery — serve o binário da
// foto por endpoint dedicado, nunca embutido no CatalogoEpcDto de listagem (só o flag TemFoto).
public record ObterFotoCatalogoEpcQuery(Guid CatalogoEpcId) : IRequest<FotoCatalogoEpcResultado?>;

public class FotoCatalogoEpcResultado
{
    public byte[] Conteudo { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = string.Empty;
    public string NomeArquivo { get; set; } = string.Empty;
}

public class ObterFotoCatalogoEpcQueryHandler : IRequestHandler<ObterFotoCatalogoEpcQuery, FotoCatalogoEpcResultado?>
{
    private readonly IAppDbContext _db;

    public ObterFotoCatalogoEpcQueryHandler(IAppDbContext db) => _db = db;

    public async Task<FotoCatalogoEpcResultado?> Handle(ObterFotoCatalogoEpcQuery request, CancellationToken ct)
    {
        var item = await _db.CatalogoEpcs.FirstOrDefaultAsync(x => x.Id == request.CatalogoEpcId, ct);

        if (item is null || item.FotoConteudo is null || item.FotoConteudo.Length == 0) return null;

        var extensao = item.FotoContentType == "image/png" ? "png" : "jpg";
        return new FotoCatalogoEpcResultado
        {
            Conteudo = item.FotoConteudo,
            ContentType = string.IsNullOrEmpty(item.FotoContentType) ? "application/octet-stream" : item.FotoContentType,
            NomeArquivo = $"epc-{item.Id}-foto.{extensao}",
        };
    }
}

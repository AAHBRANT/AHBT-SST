using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CatalogosEpc.Queries;

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
        var epc = await _db.CatalogoEpcs.FirstOrDefaultAsync(x => x.Id == request.CatalogoEpcId, ct);

        if (epc is null || !epc.Ativo || epc.FotoConteudo is null || epc.FotoConteudo.Length == 0) return null;

        var extensao = epc.FotoContentType == "image/png" ? "png" : "jpg";
        return new FotoCatalogoEpcResultado
        {
            Conteudo = epc.FotoConteudo,
            ContentType = string.IsNullOrEmpty(epc.FotoContentType) ? "application/octet-stream" : epc.FotoContentType,
            NomeArquivo = $"epc-{epc.Id}-foto.{extensao}",
        };
    }
}

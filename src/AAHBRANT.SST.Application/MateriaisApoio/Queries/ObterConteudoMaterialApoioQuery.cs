using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.MateriaisApoio.Queries;

public record ObterConteudoMaterialApoioQuery(Guid Id) : IRequest<ConteudoMaterialApoioResultado?>;

public class ConteudoMaterialApoioResultado
{
    public byte[] Conteudo { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = string.Empty;
    public string NomeArquivo { get; set; } = string.Empty;
}

public class ObterConteudoMaterialApoioQueryHandler : IRequestHandler<ObterConteudoMaterialApoioQuery, ConteudoMaterialApoioResultado?>
{
    private readonly IAppDbContext _db;

    public ObterConteudoMaterialApoioQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ConteudoMaterialApoioResultado?> Handle(ObterConteudoMaterialApoioQuery request, CancellationToken ct)
    {
        var material = await _db.MateriaisApoio.FirstOrDefaultAsync(m => m.Id == request.Id, ct);
        if (material is null) return null;

        return new ConteudoMaterialApoioResultado
        {
            Conteudo = material.Conteudo,
            ContentType = material.ContentType,
            NomeArquivo = material.NomeArquivo,
        };
    }
}

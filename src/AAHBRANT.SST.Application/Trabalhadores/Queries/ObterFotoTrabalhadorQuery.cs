using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Trabalhadores.Queries;

public record ObterFotoTrabalhadorQuery(Guid TrabalhadorId) : IRequest<FotoTrabalhadorResultado?>;

public class FotoTrabalhadorResultado
{
    public byte[] Conteudo { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = string.Empty;
    public string NomeArquivo { get; set; } = string.Empty;
}

public class ObterFotoTrabalhadorQueryHandler : IRequestHandler<ObterFotoTrabalhadorQuery, FotoTrabalhadorResultado?>
{
    private readonly IAppDbContext _db;

    public ObterFotoTrabalhadorQueryHandler(IAppDbContext db) => _db = db;

    public async Task<FotoTrabalhadorResultado?> Handle(ObterFotoTrabalhadorQuery request, CancellationToken ct)
    {
        var trabalhador = await _db.Trabalhadores
            .FirstOrDefaultAsync(t => t.Id == request.TrabalhadorId, ct);

        if (trabalhador is null || trabalhador.FotoConteudo is null || trabalhador.FotoConteudo.Length == 0) return null;

        var extensao = trabalhador.FotoContentType == "image/png" ? "png" : "jpg";
        return new FotoTrabalhadorResultado
        {
            Conteudo = trabalhador.FotoConteudo,
            ContentType = string.IsNullOrEmpty(trabalhador.FotoContentType) ? "application/octet-stream" : trabalhador.FotoContentType,
            NomeArquivo = $"trabalhador-{trabalhador.Id}-foto.{extensao}",
        };
    }
}

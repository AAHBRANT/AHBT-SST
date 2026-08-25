using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Higienizacao.Queries;

public record ObterFotoRegistroHigienizacaoQuery(Guid RegistroId) : IRequest<FotoRegistroHigienizacaoResultado?>;

public class FotoRegistroHigienizacaoResultado
{
    public byte[] Conteudo { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = string.Empty;
    public string NomeArquivo { get; set; } = string.Empty;
}

public class ObterFotoRegistroHigienizacaoQueryHandler : IRequestHandler<ObterFotoRegistroHigienizacaoQuery, FotoRegistroHigienizacaoResultado?>
{
    private readonly IAppDbContext _db;

    public ObterFotoRegistroHigienizacaoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<FotoRegistroHigienizacaoResultado?> Handle(ObterFotoRegistroHigienizacaoQuery request, CancellationToken ct)
    {
        var registro = await _db.RegistrosHigienizacao
            .FirstOrDefaultAsync(r => r.Id == request.RegistroId, ct);

        if (registro is null || registro.FotoConteudo.Length == 0) return null;

        var extensao = registro.FotoContentType == "image/png" ? "png" : "jpg";
        return new FotoRegistroHigienizacaoResultado
        {
            Conteudo = registro.FotoConteudo,
            ContentType = string.IsNullOrEmpty(registro.FotoContentType) ? "application/octet-stream" : registro.FotoContentType,
            NomeArquivo = $"higienizacao-{registro.Id}.{extensao}",
        };
    }
}

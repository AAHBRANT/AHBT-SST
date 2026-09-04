using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.SessoesTreinamento.Queries;

public record ObterFotoEvidenciaSessaoTreinamentoQuery(Guid FotoId) : IRequest<FotoEvidenciaSessaoTreinamentoResultado?>;

public class FotoEvidenciaSessaoTreinamentoResultado
{
    public byte[] Conteudo { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = string.Empty;
    public string NomeArquivo { get; set; } = string.Empty;
}

public class ObterFotoEvidenciaSessaoTreinamentoQueryHandler : IRequestHandler<ObterFotoEvidenciaSessaoTreinamentoQuery, FotoEvidenciaSessaoTreinamentoResultado?>
{
    private readonly IAppDbContext _db;
    public ObterFotoEvidenciaSessaoTreinamentoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<FotoEvidenciaSessaoTreinamentoResultado?> Handle(ObterFotoEvidenciaSessaoTreinamentoQuery request, CancellationToken ct)
    {
        var foto = await _db.FotosEvidenciaSessaoTreinamento.FirstOrDefaultAsync(f => f.Id == request.FotoId, ct);
        if (foto is null) return null;

        var extensao = foto.FotoContentType == "image/png" ? "png" : "jpg";
        return new FotoEvidenciaSessaoTreinamentoResultado
        {
            Conteudo = foto.FotoConteudo,
            ContentType = string.IsNullOrEmpty(foto.FotoContentType) ? "application/octet-stream" : foto.FotoContentType,
            NomeArquivo = $"turma-{foto.SessaoTreinamentoId}-evidencia-{foto.Ordem}.{extensao}",
        };
    }
}

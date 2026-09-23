using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Treinamentos.Queries;

// Devolve o certificado digitalizado para visualizar/baixar (22/09) — mesmo formato de resultado de
// ObterFotoEvidenciaSessaoTreinamentoQuery.
public record ObterArquivoCertificadoTreinamentoQuery(Guid TreinamentoId) : IRequest<ArquivoCertificadoTreinamentoResultado?>;

public class ArquivoCertificadoTreinamentoResultado
{
    public byte[] Conteudo { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = string.Empty;
    public string NomeArquivo { get; set; } = string.Empty;
}

public class ObterArquivoCertificadoTreinamentoQueryHandler : IRequestHandler<ObterArquivoCertificadoTreinamentoQuery, ArquivoCertificadoTreinamentoResultado?>
{
    private readonly IAppDbContext _db;
    public ObterArquivoCertificadoTreinamentoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ArquivoCertificadoTreinamentoResultado?> Handle(ObterArquivoCertificadoTreinamentoQuery request, CancellationToken ct)
    {
        var arquivo = await _db.ArquivosCertificadoTreinamento
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.TreinamentoId == request.TreinamentoId, ct);
        if (arquivo is null) return null;

        return new ArquivoCertificadoTreinamentoResultado
        {
            Conteudo = arquivo.Conteudo,
            ContentType = string.IsNullOrEmpty(arquivo.ContentType) ? "application/octet-stream" : arquivo.ContentType,
            NomeArquivo = arquivo.NomeArquivo,
        };
    }
}

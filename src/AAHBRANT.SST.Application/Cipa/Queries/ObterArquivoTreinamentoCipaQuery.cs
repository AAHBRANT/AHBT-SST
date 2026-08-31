using AAHBRANT.SST.Application.Cipa.Commands;
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cipa.Queries;

public class ArquivoTreinamentoCipaResultado
{
    public byte[] Conteudo { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = string.Empty;
    public string NomeArquivo { get; set; } = string.Empty;
}

public record ObterArquivoTreinamentoCipaQuery(Guid TreinamentoId, TipoArquivoTreinamentoCipa Tipo) : IRequest<ArquivoTreinamentoCipaResultado?>;

public class ObterArquivoTreinamentoCipaQueryHandler : IRequestHandler<ObterArquivoTreinamentoCipaQuery, ArquivoTreinamentoCipaResultado?>
{
    private readonly IAppDbContext _db;
    public ObterArquivoTreinamentoCipaQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ArquivoTreinamentoCipaResultado?> Handle(ObterArquivoTreinamentoCipaQuery request, CancellationToken ct)
    {
        var treinamento = await _db.TreinamentosCipa.FirstOrDefaultAsync(t => t.Id == request.TreinamentoId, ct);
        if (treinamento is null) return null;

        var (conteudo, contentType, sufixo) = request.Tipo == TipoArquivoTreinamentoCipa.Certificado
            ? (treinamento.CertificadoConteudo, treinamento.CertificadoContentType, "certificado")
            : (treinamento.ListaPresencaConteudo, treinamento.ListaPresencaContentType, "lista-presenca");

        if (conteudo is null || contentType is null) return null;

        var extensao = contentType == "application/pdf" ? "pdf" : contentType == "image/png" ? "png" : "jpg";
        return new ArquivoTreinamentoCipaResultado
        {
            Conteudo = conteudo,
            ContentType = contentType,
            NomeArquivo = $"cipa-treinamento-{sufixo}-{treinamento.Id}.{extensao}",
        };
    }
}

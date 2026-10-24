using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.TermosCompromissoEpi.Queries;

public record ArquivoTermoManualEpiDto(string Nome, string ContentType, byte[] Conteudo);

public record ObterArquivoTermoManualEpiQuery(Guid TrabalhadorId) : IRequest<ArquivoTermoManualEpiDto?>;

public class ObterArquivoTermoManualEpiQueryHandler : IRequestHandler<ObterArquivoTermoManualEpiQuery, ArquivoTermoManualEpiDto?>
{
    private readonly IAppDbContext _db;
    public ObterArquivoTermoManualEpiQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ArquivoTermoManualEpiDto?> Handle(ObterArquivoTermoManualEpiQuery request, CancellationToken ct)
    {
        if (!await _db.TrabalhadorNoEscopoAsync(request.TrabalhadorId, ct))
            return null;

        var arquivo = await _db.TermosCompromissoEpiManual
            .Where(t => t.TrabalhadorId == request.TrabalhadorId && t.ArquivoConteudo != null)
            .Select(t => new { t.ArquivoNome, t.ArquivoContentType, t.ArquivoConteudo })
            .FirstOrDefaultAsync(ct);
        return arquivo is null
            ? null
            : new ArquivoTermoManualEpiDto(arquivo.ArquivoNome ?? "termo.pdf", arquivo.ArquivoContentType ?? "application/octet-stream", arquivo.ArquivoConteudo!);
    }
}

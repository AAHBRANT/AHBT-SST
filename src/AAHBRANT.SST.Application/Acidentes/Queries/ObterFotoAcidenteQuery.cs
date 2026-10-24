using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Acidentes.Queries;

public record FotoAcidenteResultado(byte[] Conteudo, string ContentType);
public record ObterFotoAcidenteQuery(Guid FotoId) : IRequest<FotoAcidenteResultado?>;
public class ObterFotoAcidenteQueryHandler : IRequestHandler<ObterFotoAcidenteQuery, FotoAcidenteResultado?>
{
    private readonly IAppDbContext _db;
    public ObterFotoAcidenteQueryHandler(IAppDbContext db) => _db = db;
    public Task<FotoAcidenteResultado?> Handle(ObterFotoAcidenteQuery request, CancellationToken ct) =>
        _db.AcidentesFotos.Where(f => f.Id == request.FotoId)
            .Select(f => new FotoAcidenteResultado(f.FotoConteudo, f.FotoContentType)).FirstOrDefaultAsync(ct);
}

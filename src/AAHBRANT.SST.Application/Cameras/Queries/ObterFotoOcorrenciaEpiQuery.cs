using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cameras.Queries;

public record ObterFotoOcorrenciaEpiQuery(Guid Id) : IRequest<byte[]>;

public class ObterFotoOcorrenciaEpiQueryHandler : IRequestHandler<ObterFotoOcorrenciaEpiQuery, byte[]>
{
    private readonly IAppDbContext _db;
    public ObterFotoOcorrenciaEpiQueryHandler(IAppDbContext db) => _db = db;

    public async Task<byte[]> Handle(ObterFotoOcorrenciaEpiQuery request, CancellationToken ct)
        => await _db.OcorrenciasEpiCamera.AsNoTracking()
               .Where(o => o.Id == request.Id)
               .Select(o => o.Foto)
               .FirstOrDefaultAsync(ct)
           ?? throw new KeyNotFoundException($"Ocorrência {request.Id} não encontrada.");
}

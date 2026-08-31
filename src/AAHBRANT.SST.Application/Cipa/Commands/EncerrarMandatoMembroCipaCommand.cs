using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cipa.Commands;

public record EncerrarMandatoMembroCipaCommand(Guid MembroId) : IRequest;

public class EncerrarMandatoMembroCipaCommandHandler : IRequestHandler<EncerrarMandatoMembroCipaCommand>
{
    private readonly IAppDbContext _db;

    public EncerrarMandatoMembroCipaCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(EncerrarMandatoMembroCipaCommand request, CancellationToken ct)
    {
        var membro = await _db.MembrosCipa.FirstOrDefaultAsync(m => m.Id == request.MembroId, ct)
            ?? throw new KeyNotFoundException($"Membro {request.MembroId} não encontrado.");

        _db.MembrosCipa.Remove(membro);
        await _db.SaveChangesAsync(ct);
    }
}

using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cameras.Commands;

// Exclusão lógica (Ativo=false): as ocorrências já registradas continuam no banco. A política
// "só Administrador" é aplicada no controller.
public record ExcluirCameraCommand(Guid Id) : IRequest;

public class ExcluirCameraCommandHandler : IRequestHandler<ExcluirCameraCommand>
{
    private readonly IAppDbContext _db;
    public ExcluirCameraCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirCameraCommand request, CancellationToken ct)
    {
        var camera = await _db.Cameras.FirstOrDefaultAsync(c => c.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Câmera {request.Id} não encontrada.");

        camera.Ativo = false;
        await _db.SaveChangesAsync(ct);
    }
}

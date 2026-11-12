using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cameras.Commands;

// Revoga o agente (PC perdido ou trocado): o segredo deixa de valer. Exclusão lógica (Ativo=false); o registro
// fica para auditoria. A política de acesso (Administrador do piloto) é aplicada no controller.
public record RevogarAgenteCameraCommand(Guid Id) : IRequest;

public class RevogarAgenteCameraCommandHandler : IRequestHandler<RevogarAgenteCameraCommand>
{
    private readonly IAppDbContext _db;
    public RevogarAgenteCameraCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(RevogarAgenteCameraCommand request, CancellationToken ct)
    {
        var agente = await _db.AgentesCamera.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Agente {request.Id} não encontrado.");

        agente.Ativo = false;
        await _db.SaveChangesAsync(ct);
    }
}

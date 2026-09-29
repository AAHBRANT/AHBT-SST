using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura.Commands;

// Revoga um agente (PC perdido/trocado): o dispositivo deixa de existir para o backend, então o
// segredo dele para de valer na sincronização de templates e nas assinaturas por digital. É
// exclusão lógica (Ativo = false), como o resto do sistema — o registro fica para auditoria.
public record RevogarDispositivoAgenteCommand(Guid DispositivoId) : IRequest;

public class RevogarDispositivoAgenteCommandHandler : IRequestHandler<RevogarDispositivoAgenteCommand>
{
    private readonly IAppDbContext _db;

    public RevogarDispositivoAgenteCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(RevogarDispositivoAgenteCommand request, CancellationToken ct)
    {
        var dispositivo = await _db.DispositivosAgenteBiometrico.FirstOrDefaultAsync(d => d.Id == request.DispositivoId, ct)
            ?? throw new KeyNotFoundException("Dispositivo não encontrado.");

        dispositivo.Ativo = false;
        await _db.SaveChangesAsync(ct);
    }
}

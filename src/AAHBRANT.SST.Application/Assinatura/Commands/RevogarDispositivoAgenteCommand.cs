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

    private readonly IAuditoriaService? _auditoria;

    public RevogarDispositivoAgenteCommandHandler(IAppDbContext db, IAuditoriaService? auditoria = null)
    {
        _db = db;
        _auditoria = auditoria;
    }

    public async Task Handle(RevogarDispositivoAgenteCommand request, CancellationToken ct)
    {
        // Só revoga leitor de obra que o usuário enxerga (auditoria 19/10, A02).
        var obras = _db.ObrasNoEscopo;
        var global = _db.EscopoObraGlobal;
        var dispositivo = await _db.DispositivosAgenteBiometrico
                .FirstOrDefaultAsync(d => d.Id == request.DispositivoId && (global || obras.Contains(d.ObraId)), ct)
            ?? throw new KeyNotFoundException("Dispositivo não encontrado.");

        dispositivo.Ativo = false;
        // Revogar um leitor é evento de segurança: fica na trilha de auditoria (antes não ficava registro).
        if (_auditoria is not null)
            await _auditoria.RegistrarAsync("DispositivoAgente.Revogado", "DispositivoAgenteBiometrico", dispositivo.Id,
                usuarioId: null, trabalhadorId: null, dadosDepois: new { dispositivo.Nome, dispositivo.ObraId }, ct);
        await _db.SaveChangesAsync(ct);
    }
}

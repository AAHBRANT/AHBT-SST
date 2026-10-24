using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PlanosIntegracao.Queries;

// "Assumir" (vários técnicos na mesma obra): o primeiro fica como responsável e os demais veem quem
// está cuidando. Quem já assumiu pode assumir de novo (no-op); outro técnico assume por cima, porque o
// responsável pode ter saído de férias — a troca fica registrada em UpdatedBy/UpdatedAtUtc.
public record AssumirPlanoIntegracaoCommand(Guid PlanoId, string? AzureAdObjectId) : IRequest;

public class AssumirPlanoIntegracaoCommandHandler : IRequestHandler<AssumirPlanoIntegracaoCommand>
{
    private readonly IAppDbContext _db;
    public AssumirPlanoIntegracaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AssumirPlanoIntegracaoCommand request, CancellationToken ct)
    {
        var plano = await _db.PlanosIntegracao.FirstOrDefaultAsync(p => p.Id == request.PlanoId, ct)
            ?? throw new KeyNotFoundException("Plano de integração não encontrado.");
        var usuarioId = string.IsNullOrWhiteSpace(request.AzureAdObjectId)
            ? (Guid?)null
            : await _db.Usuarios.Where(u => u.AzureAdObjectId == request.AzureAdObjectId).Select(u => (Guid?)u.Id).FirstOrDefaultAsync(ct);
        if (usuarioId is null)
            throw new InvalidOperationException("Entre com sua conta para assumir o plano de integração.");
        if (plano.ResponsavelUsuarioId == usuarioId) return;

        plano.ResponsavelUsuarioId = usuarioId;
        plano.AssumidoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}

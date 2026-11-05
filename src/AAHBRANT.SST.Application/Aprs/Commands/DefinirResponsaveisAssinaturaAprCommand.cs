using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Aprs.Commands;

// Quem elabora e quem supervisiona a APR (05/11, pedido do usuário): todos assinam por biometria e o
// quiosque grava o papel a partir daqui. Dá para definir também em APRs já criadas (não há tela de edição).
// Quem já assinou no papel não pode ser trocado — a assinatura ficaria atribuída a outra pessoa.
public record DefinirResponsaveisAssinaturaAprCommand(Guid AprId, Guid? ElaboradorTrabalhadorId, Guid? SupervisorTrabalhadorId) : IRequest;

public class DefinirResponsaveisAssinaturaAprCommandHandler : IRequestHandler<DefinirResponsaveisAssinaturaAprCommand>
{
    private readonly IAppDbContext _db;

    public DefinirResponsaveisAssinaturaAprCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(DefinirResponsaveisAssinaturaAprCommand request, CancellationToken ct)
    {
        await _db.GarantirAprNoEscopoAsync(request.AprId, ct);
        var apr = await _db.Aprs.FirstOrDefaultAsync(a => a.Id == request.AprId, ct)
            ?? throw new KeyNotFoundException($"APR {request.AprId} não encontrada.");

        var papeisAssinados = await _db.DocumentoSignatarios
            .Where(s => s.DocumentoAssinatura!.EntidadeTipo == nameof(Domain.Entidades.Apr) && s.DocumentoAssinatura.EntidadeId == apr.Id)
            .Select(s => s.Papel)
            .ToListAsync(ct);
        if (apr.ElaboradorTrabalhadorId != request.ElaboradorTrabalhadorId && papeisAssinados.Contains(Domain.Enums.PapelAssinatura.AprElaboracao))
            throw new InvalidOperationException("A elaboração já foi assinada — não é possível trocar quem elabora.");
        if (apr.SupervisorTrabalhadorId != request.SupervisorTrabalhadorId && papeisAssinados.Contains(Domain.Enums.PapelAssinatura.AprSupervisao))
            throw new InvalidOperationException("A supervisão já foi assinada — não é possível trocar quem supervisiona.");

        foreach (var id in new[] { request.ElaboradorTrabalhadorId, request.SupervisorTrabalhadorId }.OfType<Guid>())
            if (!await _db.Trabalhadores.AnyAsync(t => t.Id == id, ct))
                throw new KeyNotFoundException($"Trabalhador {id} não encontrado.");

        apr.ElaboradorTrabalhadorId = request.ElaboradorTrabalhadorId;
        apr.SupervisorTrabalhadorId = request.SupervisorTrabalhadorId;
        await _db.SaveChangesAsync(ct);
    }
}

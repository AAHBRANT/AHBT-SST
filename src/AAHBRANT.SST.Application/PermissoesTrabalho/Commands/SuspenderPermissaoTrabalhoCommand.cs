using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PermissoesTrabalho.Commands;

// §8 "Suspensão" — a PT perde a validade em caso de mudança de escopo/local/equipe crítica,
// condições ambientais, incidente, emergência, interrupção prolongada, alteração das condições de
// risco ou vencimento do período (texto literal do documento) — só permitida a partir de uma PT
// autorizada.
public record SuspenderPermissaoTrabalhoCommand(Guid Id, string Motivo, Guid SuspensaPorUsuarioId) : IRequest;

public class SuspenderPermissaoTrabalhoCommandValidator : AbstractValidator<SuspenderPermissaoTrabalhoCommand>
{
    public SuspenderPermissaoTrabalhoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Motivo).NotEmpty().MaximumLength(500);
        RuleFor(x => x.SuspensaPorUsuarioId).NotEmpty();
    }
}

public class SuspenderPermissaoTrabalhoCommandHandler : IRequestHandler<SuspenderPermissaoTrabalhoCommand>
{
    private readonly IAppDbContext _db;

    public SuspenderPermissaoTrabalhoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(SuspenderPermissaoTrabalhoCommand request, CancellationToken ct)
    {
        var pt = await _db.PermissoesTrabalho.FirstOrDefaultAsync(p => p.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Permissão de Trabalho {request.Id} não encontrada.");

        if (pt.Status != StatusPt.Autorizada)
            throw new InvalidOperationException("Só é possível suspender uma PT autorizada.");

        var usuarioExiste = await _db.Usuarios.AnyAsync(u => u.Id == request.SuspensaPorUsuarioId, ct);
        if (!usuarioExiste)
            throw new KeyNotFoundException($"Usuário {request.SuspensaPorUsuarioId} não encontrado.");

        pt.Status = StatusPt.Suspensa;
        pt.MotivoSuspensao = request.Motivo;
        pt.SuspensaPorUsuarioId = request.SuspensaPorUsuarioId;
        pt.DataSuspensao = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }
}

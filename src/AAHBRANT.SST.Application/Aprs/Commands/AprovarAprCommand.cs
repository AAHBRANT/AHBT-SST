using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Aprs.Commands;

// "Aprovação" (§17) — ação dedicada em vez de edição genérica de Status, para deixar o gate
// de liberação da atividade (§46) explícito e auditável (quem aprovou e quando).
public record AprovarAprCommand(Guid Id, Guid AprovadoPorUsuarioId) : IRequest;

public class AprovarAprCommandValidator : AbstractValidator<AprovarAprCommand>
{
    public AprovarAprCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.AprovadoPorUsuarioId).NotEmpty();
    }
}

public class AprovarAprCommandHandler : IRequestHandler<AprovarAprCommand>
{
    private readonly IAppDbContext _db;

    public AprovarAprCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AprovarAprCommand request, CancellationToken ct)
    {
        var apr = await _db.Aprs.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"APR {request.Id} não encontrada.");

        var usuarioExiste = await _db.Usuarios.AnyAsync(u => u.Id == request.AprovadoPorUsuarioId, ct);
        if (!usuarioExiste)
            throw new KeyNotFoundException($"Usuário {request.AprovadoPorUsuarioId} não encontrado.");

        apr.Status = StatusApr.Aprovada;
        apr.AprovadoPorUsuarioId = request.AprovadoPorUsuarioId;
        apr.DataAprovacao = DateTime.UtcNow;
        apr.MotivoReprovacao = null;

        await _db.SaveChangesAsync(ct);
    }
}

using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PermissoesTrabalho.Commands;

// "Encerramento" (§18) — ação dedicada, só permitida a partir de uma PT já autorizada
// (mesmo padrão de gate de transição de estado usado em AprovarAprCommand/ReprovarAprCommand).
public record EncerrarPermissaoTrabalhoCommand(
    Guid Id,
    Guid EncerradaPorUsuarioId,
    string? Observacoes) : IRequest;

public class EncerrarPermissaoTrabalhoCommandValidator : AbstractValidator<EncerrarPermissaoTrabalhoCommand>
{
    public EncerrarPermissaoTrabalhoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.EncerradaPorUsuarioId).NotEmpty();
        RuleFor(x => x.Observacoes).MaximumLength(500);
    }
}

public class EncerrarPermissaoTrabalhoCommandHandler : IRequestHandler<EncerrarPermissaoTrabalhoCommand>
{
    private readonly IAppDbContext _db;

    public EncerrarPermissaoTrabalhoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(EncerrarPermissaoTrabalhoCommand request, CancellationToken ct)
    {
        var pt = await _db.PermissoesTrabalho.FirstOrDefaultAsync(p => p.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Permissão de Trabalho {request.Id} não encontrada.");

        if (pt.Status != StatusPt.Autorizada)
            throw new InvalidOperationException("Só é possível encerrar uma PT autorizada.");

        var usuarioExiste = await _db.Usuarios.AnyAsync(u => u.Id == request.EncerradaPorUsuarioId, ct);
        if (!usuarioExiste)
            throw new KeyNotFoundException($"Usuário {request.EncerradaPorUsuarioId} não encontrado.");

        pt.Status = StatusPt.Encerrada;
        pt.EncerradaPorUsuarioId = request.EncerradaPorUsuarioId;
        pt.DataEncerramento = DateTime.UtcNow;
        pt.ObservacoesEncerramento = request.Observacoes;

        await _db.SaveChangesAsync(ct);
    }
}

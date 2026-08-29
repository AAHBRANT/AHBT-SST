using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PermissoesTrabalho.Commands;

// §8 "Encerramento" — "área inspecionada, limpa, segura e liberada" (texto literal do documento).
// Permitida a partir de Autorizada ou Suspensa (uma PT suspensa pode ser encerrada diretamente, sem
// precisar ser revalidada antes).
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

        if (pt.Status is not (StatusPt.Autorizada or StatusPt.Suspensa))
            throw new InvalidOperationException("Só é possível encerrar uma PT autorizada ou suspensa.");

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

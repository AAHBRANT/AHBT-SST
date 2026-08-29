using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PermissoesTrabalho.Commands;

// §8 "Revalidação" — "condições reavaliadas e mantidas. Nova validade até: ___ Hora: ___" (texto
// literal do documento). Permitida a partir de Suspensa (retomada após correção) ou de Autorizada
// (revalidação antes do vencimento, sem ter passado por suspensão) — em ambos os casos volta/segue
// como Autorizada com a nova validade.
public record RevalidarPermissaoTrabalhoCommand(
    Guid Id,
    DateTime NovaValidade,
    TimeSpan? NovoHorarioFim,
    Guid RevalidadaPorUsuarioId) : IRequest;

public class RevalidarPermissaoTrabalhoCommandValidator : AbstractValidator<RevalidarPermissaoTrabalhoCommand>
{
    public RevalidarPermissaoTrabalhoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.RevalidadaPorUsuarioId).NotEmpty();
    }
}

public class RevalidarPermissaoTrabalhoCommandHandler : IRequestHandler<RevalidarPermissaoTrabalhoCommand>
{
    private readonly IAppDbContext _db;

    public RevalidarPermissaoTrabalhoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(RevalidarPermissaoTrabalhoCommand request, CancellationToken ct)
    {
        var pt = await _db.PermissoesTrabalho.FirstOrDefaultAsync(p => p.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Permissão de Trabalho {request.Id} não encontrada.");

        if (pt.Status is not (StatusPt.Autorizada or StatusPt.Suspensa))
            throw new InvalidOperationException("Só é possível revalidar uma PT autorizada ou suspensa.");

        var usuarioExiste = await _db.Usuarios.AnyAsync(u => u.Id == request.RevalidadaPorUsuarioId, ct);
        if (!usuarioExiste)
            throw new KeyNotFoundException($"Usuário {request.RevalidadaPorUsuarioId} não encontrado.");

        pt.Status = StatusPt.Autorizada;
        pt.Validade = request.NovaValidade;
        if (request.NovoHorarioFim.HasValue)
            pt.HorarioFim = request.NovoHorarioFim;
        pt.RevalidadaPorUsuarioId = request.RevalidadaPorUsuarioId;
        pt.DataRevalidacao = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }
}

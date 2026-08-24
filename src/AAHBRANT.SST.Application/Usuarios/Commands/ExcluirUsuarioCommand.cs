using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Usuarios.Commands;

public record ExcluirUsuarioCommand(Guid Id) : IRequest;

public class ExcluirUsuarioCommandValidator : AbstractValidator<ExcluirUsuarioCommand>
{
    public ExcluirUsuarioCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirUsuarioCommandHandler : IRequestHandler<ExcluirUsuarioCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirUsuarioCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirUsuarioCommand request, CancellationToken ct)
    {
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Usuário {request.Id} não encontrado.");

        // Soft-delete (via AplicarAuditoria do SstDbContext) — nunca hard delete de registro crítico.
        _db.Usuarios.Remove(usuario);
        await _db.SaveChangesAsync(ct);
    }
}

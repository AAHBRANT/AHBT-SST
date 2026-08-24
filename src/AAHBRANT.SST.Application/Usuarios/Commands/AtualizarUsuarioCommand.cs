using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Usuarios.Commands;

public record AtualizarUsuarioCommand(
    Guid Id,
    string Nome,
    StatusUsuario Status,
    Guid? TrabalhadorId) : IRequest;

public class AtualizarUsuarioCommandValidator : AbstractValidator<AtualizarUsuarioCommand>
{
    public AtualizarUsuarioCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
    }
}

public class AtualizarUsuarioCommandHandler : IRequestHandler<AtualizarUsuarioCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarUsuarioCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarUsuarioCommand request, CancellationToken ct)
    {
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Usuário {request.Id} não encontrado.");

        usuario.Nome = request.Nome;
        usuario.Status = request.Status;
        usuario.TrabalhadorId = request.TrabalhadorId;

        await _db.SaveChangesAsync(ct);
    }
}

using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Usuarios.Commands;

// Não recebe senha/hash: autenticação é 100% Entra ID SSO. AzureAdObjectId é o vínculo
// com a identidade real, obtido do claim `oid` do token — aqui é só o cadastro administrativo
// (pré-provisionamento de acesso antes do primeiro login, ou vínculo manual pelo Administrador).
public record CriarUsuarioCommand(
    string AzureAdObjectId,
    string Email,
    string Nome,
    Guid? TrabalhadorId) : IRequest<Guid>;

public class CriarUsuarioCommandValidator : AbstractValidator<CriarUsuarioCommand>
{
    public CriarUsuarioCommandValidator()
    {
        RuleFor(x => x.AzureAdObjectId).NotEmpty().MaximumLength(36);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
    }
}

public class CriarUsuarioCommandHandler : IRequestHandler<CriarUsuarioCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarUsuarioCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarUsuarioCommand request, CancellationToken ct)
    {
        var jaExiste = await _db.Usuarios.AnyAsync(
            u => u.AzureAdObjectId == request.AzureAdObjectId || u.Email == request.Email, ct);
        if (jaExiste)
            throw new InvalidOperationException("Já existe um usuário com este AzureAdObjectId ou Email.");

        var usuario = new Usuario
        {
            AzureAdObjectId = request.AzureAdObjectId,
            Email = request.Email,
            Nome = request.Nome,
            TrabalhadorId = request.TrabalhadorId
        };

        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync(ct);
        return usuario.Id;
    }
}

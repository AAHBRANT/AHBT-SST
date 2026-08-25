using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Usuarios.Commands;

// Não recebe senha/hash: autenticação é 100% Entra ID SSO. AzureAdObjectId NÃO é digitado aqui —
// o Administrador não tem como conhecer o Object Id de outra identidade no Entra ID. Este comando
// faz o pré-cadastro só por Email/Nome; o vínculo com o claim `oid` é preenchido automaticamente
// no primeiro login via Teams (ver VinculoAzureAdMiddleware), casando pelo Email.
public record CriarUsuarioCommand(
    string Email,
    string Nome,
    Guid? TrabalhadorId) : IRequest<Guid>;

public class CriarUsuarioCommandValidator : AbstractValidator<CriarUsuarioCommand>
{
    public CriarUsuarioCommandValidator()
    {
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
        var jaExiste = await _db.Usuarios.AnyAsync(u => u.Email == request.Email, ct);
        if (jaExiste)
            throw new InvalidOperationException("Já existe um usuário com este Email.");

        var usuario = new Usuario
        {
            Email = request.Email,
            Nome = request.Nome,
            TrabalhadorId = request.TrabalhadorId
        };

        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync(ct);
        return usuario.Id;
    }
}

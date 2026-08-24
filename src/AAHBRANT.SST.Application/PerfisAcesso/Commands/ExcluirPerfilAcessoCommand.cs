using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PerfisAcesso.Commands;

public record ExcluirPerfilAcessoCommand(Guid Id) : IRequest;

public class ExcluirPerfilAcessoCommandValidator : AbstractValidator<ExcluirPerfilAcessoCommand>
{
    public ExcluirPerfilAcessoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirPerfilAcessoCommandHandler : IRequestHandler<ExcluirPerfilAcessoCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirPerfilAcessoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirPerfilAcessoCommand request, CancellationToken ct)
    {
        var perfil = await _db.PerfisAcesso.FirstOrDefaultAsync(p => p.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Perfil de acesso {request.Id} não encontrado.");

        // Espec. do usuário (is_system): perfis de sistema (os 12 da §44) não podem ser excluídos.
        if (perfil.EhSistema)
            throw new InvalidOperationException("Perfis de sistema não podem ser excluídos — apenas suas permissões podem ser reconfiguradas.");

        _db.PerfisAcesso.Remove(perfil);
        await _db.SaveChangesAsync(ct);
    }
}

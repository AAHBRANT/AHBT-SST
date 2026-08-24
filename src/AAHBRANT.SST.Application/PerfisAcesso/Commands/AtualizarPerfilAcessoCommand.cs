using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PerfisAcesso.Commands;

// Perfis de sistema (EhSistema = true) podem ter Nome/Descrição ajustados, mas seu Tipo
// e a garantia EhSistema nunca mudam por aqui (só a matriz de permissões é reconfigurável).
public record AtualizarPerfilAcessoCommand(Guid Id, string Nome, string? Descricao) : IRequest;

public class AtualizarPerfilAcessoCommandValidator : AbstractValidator<AtualizarPerfilAcessoCommand>
{
    public AtualizarPerfilAcessoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(80);
    }
}

public class AtualizarPerfilAcessoCommandHandler : IRequestHandler<AtualizarPerfilAcessoCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarPerfilAcessoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarPerfilAcessoCommand request, CancellationToken ct)
    {
        var perfil = await _db.PerfisAcesso.FirstOrDefaultAsync(p => p.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Perfil de acesso {request.Id} não encontrado.");

        perfil.Nome = request.Nome;
        perfil.Descricao = request.Descricao;

        await _db.SaveChangesAsync(ct);
    }
}

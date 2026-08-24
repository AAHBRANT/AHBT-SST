using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PerfisAcesso.Commands;

// Sempre cria perfil customizado (Tipo = null, EhSistema = false) — os 12 perfis de sistema
// da §44 são exclusivamente semeados na inicialização (ver Infrastructure/Persistencia/Seed),
// nunca criados via API, para preservar a garantia de que Tipo é único entre eles.
public record CriarPerfilAcessoCommand(string Nome, string? Descricao) : IRequest<Guid>;

public class CriarPerfilAcessoCommandValidator : AbstractValidator<CriarPerfilAcessoCommand>
{
    public CriarPerfilAcessoCommandValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(80);
    }
}

public class CriarPerfilAcessoCommandHandler : IRequestHandler<CriarPerfilAcessoCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarPerfilAcessoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarPerfilAcessoCommand request, CancellationToken ct)
    {
        var jaExiste = await _db.PerfisAcesso.AnyAsync(p => p.Nome == request.Nome, ct);
        if (jaExiste)
            throw new InvalidOperationException($"Já existe um perfil de acesso com o nome '{request.Nome}'.");

        var perfil = new PerfilAcesso
        {
            Tipo = null,
            EhSistema = false,
            Nome = request.Nome,
            Descricao = request.Descricao
        };

        _db.PerfisAcesso.Add(perfil);
        await _db.SaveChangesAsync(ct);
        return perfil.Id;
    }
}

using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Usuarios.Commands;

// Vínculo Usuário x Perfil x Obra (ObraId nulo = perfil de escopo Global/Unidade, não restrito
// a uma obra). É esta tabela — não o token do Entra ID — que resolve o escopo por obra
// (docs/RBAC-Matrix.md §4: "o `roles` do JWT resolve o perfil; UsuarioPerfilObra resolve o escopo").
public record AtribuirPerfilObraCommand(
    Guid UsuarioId,
    Guid PerfilAcessoId,
    Guid? ObraId) : IRequest<Guid>;

public class AtribuirPerfilObraCommandValidator : AbstractValidator<AtribuirPerfilObraCommand>
{
    public AtribuirPerfilObraCommandValidator()
    {
        RuleFor(x => x.UsuarioId).NotEmpty();
        RuleFor(x => x.PerfilAcessoId).NotEmpty();
    }
}

public class AtribuirPerfilObraCommandHandler : IRequestHandler<AtribuirPerfilObraCommand, Guid>
{
    private readonly IAppDbContext _db;

    public AtribuirPerfilObraCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(AtribuirPerfilObraCommand request, CancellationToken ct)
    {
        var usuarioExiste = await _db.Usuarios.AnyAsync(u => u.Id == request.UsuarioId, ct);
        if (!usuarioExiste)
            throw new KeyNotFoundException($"Usuário {request.UsuarioId} não encontrado.");

        var perfilExiste = await _db.PerfisAcesso.AnyAsync(p => p.Id == request.PerfilAcessoId, ct);
        if (!perfilExiste)
            throw new KeyNotFoundException($"Perfil de acesso {request.PerfilAcessoId} não encontrado.");

        var jaAtribuido = await _db.UsuariosPerfilObra.AnyAsync(
            x => x.UsuarioId == request.UsuarioId
              && x.PerfilAcessoId == request.PerfilAcessoId
              && x.ObraId == request.ObraId, ct);
        if (jaAtribuido)
            throw new InvalidOperationException("Este usuário já possui este perfil neste escopo de obra.");

        var vinculo = new UsuarioPerfilObra
        {
            UsuarioId = request.UsuarioId,
            PerfilAcessoId = request.PerfilAcessoId,
            ObraId = request.ObraId
        };

        _db.UsuariosPerfilObra.Add(vinculo);
        await _db.SaveChangesAsync(ct);
        return vinculo.Id;
    }
}

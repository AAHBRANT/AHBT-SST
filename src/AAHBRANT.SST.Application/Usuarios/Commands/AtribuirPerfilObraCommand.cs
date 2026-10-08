using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
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
    Guid? ObraId,
    // Preenchidos SEMPRE pelo controller a partir do token (nunca confiar no corpo da requisição):
    // quem está concedendo o perfil e se a autenticação Entra ID está ligada. Auditoria 06/10/2026
    // (A1): sem isto, qualquer detentor de usuario:editar atribuía o perfil Administrador a si mesmo.
    string? SolicitanteAzureAdObjectId = null,
    bool AutenticacaoHabilitada = false) : IRequest<Guid>;

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
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == request.UsuarioId, ct)
            ?? throw new KeyNotFoundException($"Usuário {request.UsuarioId} não encontrado.");

        var perfil = await _db.PerfisAcesso.FirstOrDefaultAsync(p => p.Id == request.PerfilAcessoId, ct)
            ?? throw new KeyNotFoundException($"Perfil de acesso {request.PerfilAcessoId} não encontrado.");

        if (request.ObraId.HasValue
            && !await _db.Obras.AnyAsync(o => o.Id == request.ObraId.Value, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        // Com a autenticação ligada, só Administrador concede o perfil Administrador, e ninguém
        // (exceto Administrador) concede perfil a si mesmo. Com ela desligada (desenvolvimento) o
        // servidor não distingue usuários, então a regra não se aplica — mesmo comportamento de
        // PermissaoAuthorizationHandler.
        if (request.AutenticacaoHabilitada)
        {
            var solicitante = request.SolicitanteAzureAdObjectId;
            var ehAdministrador = !string.IsNullOrWhiteSpace(solicitante)
                && await _db.Usuarios
                    .Where(u => u.AzureAdObjectId == solicitante && u.Status == StatusUsuario.Ativo)
                    .SelectMany(u => u.PerfisPorObra)
                    .AnyAsync(v => v.PerfilAcesso != null
                        && v.PerfilAcesso.Tipo == TipoPerfilAcesso.Administrador, ct);

            if (!ehAdministrador)
            {
                if (perfil.Tipo == TipoPerfilAcesso.Administrador)
                    throw new AcessoNegadoException();

                if (!string.IsNullOrWhiteSpace(solicitante) && usuario.AzureAdObjectId == solicitante)
                    throw new AcessoNegadoException();
            }
        }

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

using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Usuarios.Queries;

// "Quem sou eu" (23/09): até aqui o frontend não sabia quem estava logado nem o que essa pessoa
// podia fazer — toda decisão de permissão vivia só no servidor, e a tela mostrava tudo para todos.
// É o que faltava para esconder ação que o usuário não pode executar, a começar pelo botão de
// excluir registro, restrito a Administrador.
//
// A identidade NUNCA vem da rota nem do corpo: o controller resolve pelo claim do token e passa
// aqui. Sem isso, qualquer um consultaria (e exibiria) o perfil de outra pessoa.
public record ObterUsuarioLogadoQuery(string? AzureAdObjectId, bool AutenticacaoHabilitada)
    : IRequest<UsuarioLogadoDto>;

public record UsuarioLogadoDto(
    string Nome,
    string? Email,
    bool EhAdministrador,
    // Códigos de Permissao concedidos por qualquer perfil vinculado ao usuário, em qualquer obra —
    // mesma abrangência que PermissaoAuthorizationHandler usa para autorizar (Camada 1 do RBAC).
    List<string> Permissoes);

public class ObterUsuarioLogadoQueryHandler : IRequestHandler<ObterUsuarioLogadoQuery, UsuarioLogadoDto>
{
    private readonly IAppDbContext _db;
    public ObterUsuarioLogadoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<UsuarioLogadoDto> Handle(ObterUsuarioLogadoQuery request, CancellationToken ct)
    {
        // Entra ID desligado (desenvolvimento local / modo standalone): PermissaoAuthorizationHandler
        // libera qualquer policy sem checar, então a tela também precisa se comportar como acesso
        // total. Responder "não é administrador" aqui esconderia na tela botões que o servidor
        // aceitaria — o pior dos dois mundos para quem está desenvolvendo.
        if (!request.AutenticacaoHabilitada)
            return new UsuarioLogadoDto("Usuário", null, EhAdministrador: true, new List<string>());

        if (string.IsNullOrWhiteSpace(request.AzureAdObjectId))
            return new UsuarioLogadoDto(string.Empty, null, EhAdministrador: false, new List<string>());

        var usuario = await _db.Usuarios
            .AsNoTracking()
            .Where(u => u.AzureAdObjectId == request.AzureAdObjectId && u.Status == StatusUsuario.Ativo)
            .Select(u => new { u.Id, u.Nome, u.Email })
            .FirstOrDefaultAsync(ct);

        // Autenticado no Entra, mas sem cadastro de Usuario ativo aqui: não é administrador e não
        // tem permissão nenhuma — as telas tratam isso como visitante.
        if (usuario is null)
            return new UsuarioLogadoDto(string.Empty, null, EhAdministrador: false, new List<string>());

        var perfis = await _db.UsuariosPerfilObra
            .AsNoTracking()
            .Where(v => v.UsuarioId == usuario.Id && v.PerfilAcesso != null)
            .Select(v => new { v.PerfilAcessoId, Tipo = v.PerfilAcesso!.Tipo })
            .ToListAsync(ct);

        var ehAdministrador = perfis.Any(p => p.Tipo == TipoPerfilAcesso.Administrador);

        var perfilIds = perfis.Select(p => p.PerfilAcessoId).Distinct().ToList();
        var permissoes = await _db.PerfisAcessoPermissoes
            .AsNoTracking()
            .Where(pp => perfilIds.Contains(pp.PerfilAcessoId) && pp.Permitido && pp.Permissao != null)
            .Select(pp => pp.Permissao!.Codigo)
            .Distinct()
            .ToListAsync(ct);

        return new UsuarioLogadoDto(usuario.Nome, usuario.Email, ehAdministrador, permissoes);
    }
}

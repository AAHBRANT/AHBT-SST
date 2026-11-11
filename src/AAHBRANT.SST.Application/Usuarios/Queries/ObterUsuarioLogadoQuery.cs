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
    List<string> Permissoes,
    // Onde cada permissão vale (06/11/2026 — Responsável x Supervisão): a tela usa para esconder
    // criar/editar nas obras que o usuário só acompanha. Mesma regra de AcessoPorObraService.
    // Nulo = acesso total (Entra desligado).
    Dictionary<string, AlcancePermissaoDto>? Alcance = null,
    // Engenheiro de Segurança (Entrega 4 da OS): só ele confirma as atividades da função. Verdadeiro com
    // vínculo ativo desse perfil em alguma obra ou global. Entra desligado (dev) = verdadeiro, como Admin.
    bool EhEngenheiroSeguranca = false,
    // Vínculo global (sem obra): vale em todas as obras.
    bool EngenheiroSegurancaGlobal = false,
    // Obras em que ele é engenheiro por vínculo de obra. Vazio quando só é global ou não é engenheiro.
    List<Guid>? ObrasEngenheiroSeguranca = null);

public record AlcancePermissaoDto(bool Global, List<Guid> Obras);

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
            return new UsuarioLogadoDto("Usuário", null, EhAdministrador: true, new List<string>(),
                EhEngenheiroSeguranca: true, EngenheiroSegurancaGlobal: true, ObrasEngenheiroSeguranca: new List<Guid>());

        if (string.IsNullOrWhiteSpace(request.AzureAdObjectId))
            return new UsuarioLogadoDto(string.Empty, null, EhAdministrador: false, new List<string>());

        var usuario = await _db.Usuarios
            .AsNoTracking()
            .Where(u => u.Ativo && u.AzureAdObjectId == request.AzureAdObjectId && u.Status == StatusUsuario.Ativo)
            .Select(u => new { u.Id, u.Nome, u.Email })
            .FirstOrDefaultAsync(ct);

        // Autenticado no Entra, mas sem cadastro de Usuario ativo aqui: não é administrador e não
        // tem permissão nenhuma — as telas tratam isso como visitante.
        if (usuario is null)
            return new UsuarioLogadoDto(string.Empty, null, EhAdministrador: false, new List<string>());

        var perfis = await _db.UsuariosPerfilObra
            .AsNoTracking()
            .Where(v => v.Ativo && v.UsuarioId == usuario.Id && v.PerfilAcesso != null && v.PerfilAcesso.Ativo)
            .Select(v => new { v.PerfilAcessoId, v.ObraId, Tipo = v.PerfilAcesso!.Tipo })
            .ToListAsync(ct);

        var ehAdministrador = perfis.Any(p => p.Tipo == TipoPerfilAcesso.Administrador);

        var perfilIds = perfis.Select(p => p.PerfilAcessoId).Distinct().ToList();
        var linhas = await _db.PerfisAcessoPermissoes
            .AsNoTracking()
            .Where(pp => pp.Ativo && perfilIds.Contains(pp.PerfilAcessoId) && pp.Permitido && pp.Permissao != null && pp.Permissao.Ativo)
            .Select(pp => new { pp.PerfilAcessoId, pp.Permissao!.Codigo, pp.Escopo })
            .ToListAsync(ct);

        var permissoes = linhas.Select(l => l.Codigo).Distinct().ToList();

        var alcance = new Dictionary<string, AlcancePermissaoDto>();
        foreach (var codigo in permissoes)
        {
            var global = false;
            var obras = new HashSet<Guid>();
            foreach (var vinculo in perfis)
            {
                var escopos = linhas.Where(l => l.PerfilAcessoId == vinculo.PerfilAcessoId && l.Codigo == codigo).Select(l => l.Escopo).ToList();
                if (escopos.Count == 0) continue;
                if (vinculo.ObraId is null || escopos.Contains(EscopoAcesso.Global)) global = true;
                if (vinculo.ObraId is Guid obraId) obras.Add(obraId);
            }
            alcance[codigo] = new AlcancePermissaoDto(global, obras.ToList());
        }

        var engenheiro = perfis.Where(p => p.Tipo == TipoPerfilAcesso.EngenheiroSeguranca).ToList();
        var obrasEngenheiro = engenheiro.Where(p => p.ObraId is not null).Select(p => p.ObraId!.Value).Distinct().ToList();
        return new UsuarioLogadoDto(usuario.Nome, usuario.Email, ehAdministrador, permissoes, alcance,
            EhEngenheiroSeguranca: engenheiro.Count > 0,
            EngenheiroSegurancaGlobal: engenheiro.Any(p => p.ObraId is null),
            ObrasEngenheiroSeguranca: obrasEngenheiro);
    }
}

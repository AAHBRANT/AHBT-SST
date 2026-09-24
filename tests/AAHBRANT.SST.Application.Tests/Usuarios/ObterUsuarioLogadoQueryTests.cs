using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Application.Usuarios.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;

namespace AAHBRANT.SST.Application.Tests.Usuarios;

// Esta query decide o que a tela mostra para cada pessoa — a começar pelo botão de excluir
// registro, que só o Administrador enxerga. Errar para o lado permissivo expõe ação que o servidor
// vai recusar; errar para o restritivo esconde o botão de quem tem direito a ele.
public class ObterUsuarioLogadoQueryTests
{
    private const string ObjectIdEntra = "11111111-2222-3333-4444-555555555555";

    private static async Task<Usuario> SemearUsuarioAsync(
        SstDbContext db, TipoPerfilAcesso? tipoPerfil, params string[] codigosPermissao)
    {
        var usuario = new Usuario
        {
            AzureAdObjectId = ObjectIdEntra,
            Nome = "Wellington Lourenço",
            Email = "dev@aahbrant.com",
            Status = StatusUsuario.Ativo,
        };
        var perfil = new PerfilAcesso { Tipo = tipoPerfil, Nome = tipoPerfil?.ToString() ?? "Customizado", EhSistema = tipoPerfil is not null };
        db.Usuarios.Add(usuario);
        db.PerfisAcesso.Add(perfil);
        await db.SaveChangesAsync();

        db.UsuariosPerfilObra.Add(new UsuarioPerfilObra { UsuarioId = usuario.Id, PerfilAcessoId = perfil.Id });
        foreach (var codigo in codigosPermissao)
        {
            var permissao = new Permissao { Codigo = codigo, Modulo = "Epi", Acao = "Excluir", Descricao = codigo };
            db.Permissoes.Add(permissao);
            await db.SaveChangesAsync();
            db.PerfisAcessoPermissoes.Add(new PerfilAcessoPermissao
            {
                PerfilAcessoId = perfil.Id,
                PermissaoId = permissao.Id,
                Permitido = true,
            });
        }
        await db.SaveChangesAsync();
        return usuario;
    }

    [Fact]
    public async Task Handle_PerfilAdministrador_EhAdministrador()
    {
        var db = DbContextFactory.Criar();
        await SemearUsuarioAsync(db, TipoPerfilAcesso.Administrador, "epi:editar");
        var handler = new ObterUsuarioLogadoQueryHandler(db);

        var eu = await handler.Handle(new ObterUsuarioLogadoQuery(ObjectIdEntra, AutenticacaoHabilitada: true), default);

        Assert.True(eu.EhAdministrador);
        Assert.Equal("Wellington Lourenço", eu.Nome);
        Assert.Contains("epi:editar", eu.Permissoes);
    }

    [Fact]
    public async Task Handle_OutroPerfil_NaoEhAdministrador()
    {
        var db = DbContextFactory.Criar();
        await SemearUsuarioAsync(db, TipoPerfilAcesso.TecnicoSeguranca, "epi:editar");
        var handler = new ObterUsuarioLogadoQueryHandler(db);

        var eu = await handler.Handle(new ObterUsuarioLogadoQuery(ObjectIdEntra, AutenticacaoHabilitada: true), default);

        Assert.False(eu.EhAdministrador);
        Assert.Contains("epi:editar", eu.Permissoes);
    }

    // Usuário desativado pela tela de Controle de Acesso perde tudo, mesmo com o token do Entra
    // ainda válido no navegador dele.
    [Fact]
    public async Task Handle_UsuarioInativo_NaoEhAdministradorESemPermissoes()
    {
        var db = DbContextFactory.Criar();
        var usuario = await SemearUsuarioAsync(db, TipoPerfilAcesso.Administrador, "epi:editar");
        usuario.Status = StatusUsuario.Inativo;
        await db.SaveChangesAsync();
        var handler = new ObterUsuarioLogadoQueryHandler(db);

        var eu = await handler.Handle(new ObterUsuarioLogadoQuery(ObjectIdEntra, AutenticacaoHabilitada: true), default);

        Assert.False(eu.EhAdministrador);
        Assert.Empty(eu.Permissoes);
    }

    // Autenticado no Entra, mas sem cadastro aqui: visitante.
    [Fact]
    public async Task Handle_SemCadastroDeUsuario_NaoEhAdministrador()
    {
        var db = DbContextFactory.Criar();
        var handler = new ObterUsuarioLogadoQueryHandler(db);

        var eu = await handler.Handle(new ObterUsuarioLogadoQuery("oid-sem-cadastro", AutenticacaoHabilitada: true), default);

        Assert.False(eu.EhAdministrador);
        Assert.Empty(eu.Permissoes);
        Assert.Equal(string.Empty, eu.Nome);
    }

    [Fact]
    public async Task Handle_SemClaimDeIdentidade_NaoEhAdministrador()
    {
        var db = DbContextFactory.Criar();
        var handler = new ObterUsuarioLogadoQueryHandler(db);

        var eu = await handler.Handle(new ObterUsuarioLogadoQuery(null, AutenticacaoHabilitada: true), default);

        Assert.False(eu.EhAdministrador);
    }

    // Desenvolvimento local (Entra ID desligado): o servidor libera qualquer policy, então a tela
    // precisa se comportar do mesmo jeito — senão o botão some para quem o backend deixaria usar.
    [Fact]
    public async Task Handle_AutenticacaoDesligada_TrataComoAdministrador()
    {
        var db = DbContextFactory.Criar();
        var handler = new ObterUsuarioLogadoQueryHandler(db);

        var eu = await handler.Handle(new ObterUsuarioLogadoQuery(null, AutenticacaoHabilitada: false), default);

        Assert.True(eu.EhAdministrador);
    }
}

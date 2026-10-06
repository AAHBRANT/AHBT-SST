using AAHBRANT.SST.Application.Common.Seguranca;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Application.Usuarios.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Usuarios;

// Auditoria 06/10/2026 (A1): usuario:editar não pode virar escada para o perfil Administrador.
public class AtribuirPerfilObraCommandHandlerTests
{
    private const string OidSolicitante = "oid-solicitante";

    private static async Task<(Infrastructure.Persistencia.SstDbContext Db, Usuario Alvo, PerfilAcesso Admin, PerfilAcesso Tecnico)>
        PrepararAsync(bool solicitanteEhAdmin)
    {
        var db = DbContextFactory.Criar();
        var admin = new PerfilAcesso { Nome = "Administrador", Tipo = TipoPerfilAcesso.Administrador, EhSistema = true };
        var tecnico = new PerfilAcesso { Nome = "Técnico", Tipo = TipoPerfilAcesso.TecnicoSeguranca, EhSistema = true };
        var solicitante = new Usuario { Nome = "Solicitante", Email = "s@x.com", AzureAdObjectId = OidSolicitante };
        var alvo = new Usuario { Nome = "Alvo", Email = "a@x.com", AzureAdObjectId = "oid-alvo" };
        db.PerfisAcesso.AddRange(admin, tecnico);
        db.Usuarios.AddRange(solicitante, alvo);
        await db.SaveChangesAsync();

        db.UsuariosPerfilObra.Add(new UsuarioPerfilObra
        {
            UsuarioId = solicitante.Id,
            PerfilAcessoId = solicitanteEhAdmin ? admin.Id : tecnico.Id
        });
        await db.SaveChangesAsync();
        return (db, alvo, admin, tecnico);
    }

    [Fact]
    public async Task NaoAdministrador_NaoConcedePerfilAdministrador()
    {
        var (db, alvo, admin, _) = await PrepararAsync(solicitanteEhAdmin: false);
        var handler = new AtribuirPerfilObraCommandHandler(db);

        await Assert.ThrowsAsync<AcessoNegadoException>(() => handler.Handle(
            new AtribuirPerfilObraCommand(alvo.Id, admin.Id, null, OidSolicitante, true), default));
    }

    [Fact]
    public async Task NaoAdministrador_NaoConcedePerfilANSiMesmo()
    {
        var (db, _, _, tecnico) = await PrepararAsync(solicitanteEhAdmin: false);
        var solicitante = await db.Usuarios.FirstAsync(u => u.AzureAdObjectId == OidSolicitante);
        var handler = new AtribuirPerfilObraCommandHandler(db);

        await Assert.ThrowsAsync<AcessoNegadoException>(() => handler.Handle(
            new AtribuirPerfilObraCommand(solicitante.Id, tecnico.Id, null, OidSolicitante, true), default));
    }

    [Fact]
    public async Task Administrador_ConcedePerfilAdministrador()
    {
        var (db, alvo, admin, _) = await PrepararAsync(solicitanteEhAdmin: true);
        var handler = new AtribuirPerfilObraCommandHandler(db);

        var id = await handler.Handle(
            new AtribuirPerfilObraCommand(alvo.Id, admin.Id, null, OidSolicitante, true), default);

        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public async Task NaoAdministrador_ConcedePerfilComumAOutroUsuario()
    {
        var (db, alvo, _, tecnico) = await PrepararAsync(solicitanteEhAdmin: false);
        var handler = new AtribuirPerfilObraCommandHandler(db);

        var id = await handler.Handle(
            new AtribuirPerfilObraCommand(alvo.Id, tecnico.Id, null, OidSolicitante, true), default);

        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public async Task ObraInexistente_LancaKeyNotFound()
    {
        var (db, alvo, _, tecnico) = await PrepararAsync(solicitanteEhAdmin: true);
        var handler = new AtribuirPerfilObraCommandHandler(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(
            new AtribuirPerfilObraCommand(alvo.Id, tecnico.Id, Guid.NewGuid(), OidSolicitante, true), default));
    }

    [Fact]
    public async Task AutenticacaoDesligada_NaoAplicaARegra()
    {
        var (db, alvo, admin, _) = await PrepararAsync(solicitanteEhAdmin: false);
        var handler = new AtribuirPerfilObraCommandHandler(db);

        var id = await handler.Handle(
            new AtribuirPerfilObraCommand(alvo.Id, admin.Id, null, null, false), default);

        Assert.NotEqual(Guid.Empty, id);
    }
}

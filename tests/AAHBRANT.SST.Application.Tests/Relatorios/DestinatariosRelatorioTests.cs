using AAHBRANT.SST.Application.Relatorios;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Relatorios;

public class DestinatariosRelatorioTests
{
    private static async Task<(SstDbContext Db, Obra Sul, Obra Norte)> NovoBanco()
    {
        var db = DbContextFactory.Criar();
        var sul = new Obra { Nome = "Obra Sul", Codigo = "S" };
        var norte = new Obra { Nome = "Obra Norte", Codigo = "N" };
        db.AddRange(sul, norte);
        await db.SaveChangesAsync();
        return (db, sul, norte);
    }

    private static async Task<Usuario> NovoUsuario(SstDbContext db, string nome, StatusUsuario status = StatusUsuario.Ativo, string? azureAdId = null)
    {
        var u = new Usuario { Nome = nome, Email = $"{nome.ToLowerInvariant()}@example.test", Status = status, AzureAdObjectId = azureAdId };
        db.Usuarios.Add(u);
        await db.SaveChangesAsync();
        return u;
    }

    private static Task<Guid> Salvar(SstDbContext db, Guid usuarioId, Guid? obraId, bool presenca = true, bool boletim = false, bool ocorrencia = false) =>
        new SalvarDestinatarioRelatorioCommandHandler(db).Handle(new SalvarDestinatarioRelatorioCommand(usuarioId, obraId, presenca, boletim, ocorrencia), CancellationToken.None);

    [Fact]
    public async Task Salvar_NaoDuplicaOMesmoUsuarioNaMesmaObra_Atualiza()
    {
        var (db, sul, _) = await NovoBanco();
        var ana = await NovoUsuario(db, "Ana");

        var primeiro = await Salvar(db, ana.Id, sul.Id, presenca: true, boletim: false);
        var segundo = await Salvar(db, ana.Id, sul.Id, presenca: false, boletim: true, ocorrencia: true);

        Assert.Equal(primeiro, segundo);
        var d = await db.DestinatariosRelatorio.SingleAsync();
        Assert.Equal((false, true, true), (d.ListaPresenca, d.BoletimSemanal, d.Ocorrencia));
    }

    [Fact]
    public async Task Salvar_EmObrasDiferentes_SaoCadastrosDiferentes()
    {
        var (db, sul, norte) = await NovoBanco();
        var ana = await NovoUsuario(db, "Ana");

        await Salvar(db, ana.Id, sul.Id);
        await Salvar(db, ana.Id, norte.Id);
        await Salvar(db, ana.Id, null); // todas as obras

        Assert.Equal(3, await db.DestinatariosRelatorio.CountAsync());
    }

    [Fact]
    public async Task Salvar_UsuarioOuObraInexistente_Falha()
    {
        var (db, _, _) = await NovoBanco();
        var ana = await NovoUsuario(db, "Ana");

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Salvar(db, Guid.NewGuid(), null));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Salvar(db, ana.Id, Guid.NewGuid()));
    }

    [Fact]
    public void Validador_ExigeAoMenosUmRelatorioMarcado()
    {
        var validador = new SalvarDestinatarioRelatorioCommandValidator();

        Assert.False(validador.Validate(new SalvarDestinatarioRelatorioCommand(Guid.NewGuid(), null, false, false, false)).IsValid);
        Assert.True(validador.Validate(new SalvarDestinatarioRelatorioCommand(Guid.NewGuid(), null, false, true, false)).IsValid);
    }

    [Fact]
    public async Task Remover_TiraDaLista()
    {
        var (db, sul, _) = await NovoBanco();
        var ana = await NovoUsuario(db, "Ana");
        var id = await Salvar(db, ana.Id, sul.Id);

        await new RemoverDestinatarioRelatorioCommandHandler(db).Handle(new RemoverDestinatarioRelatorioCommand(id), CancellationToken.None);

        Assert.Empty(await new ListarDestinatariosRelatorioQueryHandler(db).Handle(new ListarDestinatariosRelatorioQuery(), CancellationToken.None));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new RemoverDestinatarioRelatorioCommandHandler(db).Handle(new RemoverDestinatarioRelatorioCommand(id), CancellationToken.None));
    }

    [Fact]
    public async Task Listar_MostraObraETemAcessoTeams_ParaAvisarQuemNaoRecebeOSininho()
    {
        var (db, sul, _) = await NovoBanco();
        var comTeams = await NovoUsuario(db, "Beatriz", azureAdId: Guid.NewGuid().ToString());
        var semTeams = await NovoUsuario(db, "Carlos");
        await Salvar(db, comTeams.Id, sul.Id);
        await Salvar(db, semTeams.Id, null);

        var lista = await new ListarDestinatariosRelatorioQueryHandler(db).Handle(new ListarDestinatariosRelatorioQuery(), CancellationToken.None);

        Assert.Equal(new[] { "Beatriz", "Carlos" }, lista.Select(l => l.UsuarioNome));
        Assert.True(lista[0].TemAcessoTeams);
        Assert.Equal("Obra Sul", lista[0].ObraNome);
        Assert.False(lista[1].TemAcessoTeams);
        Assert.Null(lista[1].ObraNome); // todas as obras
    }

    private static async Task<PerfilAcesso> NovoPerfil(SstDbContext db, TipoPerfilAcesso tipo)
    {
        var perfil = new PerfilAcesso { Tipo = tipo, Nome = tipo.ToString(), EhSistema = true };
        db.PerfisAcesso.Add(perfil);
        await db.SaveChangesAsync();
        return perfil;
    }

    [Fact]
    public async Task SugerirPorPerfil_PreenchePelosTecnicosEEngenheirosAtivos_SemDuplicar()
    {
        var (db, sul, norte) = await NovoBanco();
        var tecnico = await NovoPerfil(db, TipoPerfilAcesso.TecnicoSeguranca);
        var engenheiro = await NovoPerfil(db, TipoPerfilAcesso.EngenheiroSeguranca);
        var encarregado = await NovoPerfil(db, TipoPerfilAcesso.Encarregado);
        var t1 = await NovoUsuario(db, "Tecnico1");
        var eng = await NovoUsuario(db, "Engenheiro1");
        var enc = await NovoUsuario(db, "Encarregado1");
        var inativo = await NovoUsuario(db, "Inativo", StatusUsuario.Inativo);
        db.UsuariosPerfilObra.AddRange(
            new UsuarioPerfilObra { UsuarioId = t1.Id, PerfilAcessoId = tecnico.Id, ObraId = sul.Id },
            new UsuarioPerfilObra { UsuarioId = t1.Id, PerfilAcessoId = tecnico.Id, ObraId = norte.Id },
            new UsuarioPerfilObra { UsuarioId = eng.Id, PerfilAcessoId = engenheiro.Id, ObraId = null }, // global
            new UsuarioPerfilObra { UsuarioId = enc.Id, PerfilAcessoId = encarregado.Id, ObraId = sul.Id },
            new UsuarioPerfilObra { UsuarioId = inativo.Id, PerfilAcessoId = tecnico.Id, ObraId = sul.Id });
        await db.SaveChangesAsync();
        var sugerir = new SugerirDestinatariosPorPerfilCommandHandler(db);

        var primeira = await sugerir.Handle(new SugerirDestinatariosPorPerfilCommand(), CancellationToken.None);
        var segunda = await sugerir.Handle(new SugerirDestinatariosPorPerfilCommand(), CancellationToken.None);

        Assert.Equal(3, primeira); // t1 em Sul e Norte + engenheiro global
        Assert.Equal(0, segunda); // não duplica
        var criados = await db.DestinatariosRelatorio.ToListAsync();
        Assert.DoesNotContain(criados, c => c.UsuarioId == enc.Id || c.UsuarioId == inativo.Id);
        Assert.All(criados, c => Assert.True(c.ListaPresenca && c.BoletimSemanal && c.Ocorrencia));
        Assert.Contains(criados, c => c.UsuarioId == eng.Id && c.ObraId == null);
    }

    [Fact]
    public async Task SugerirPorPerfil_NaoMexeEmQuemJaFoiAjustadoAMao()
    {
        var (db, sul, _) = await NovoBanco();
        var tecnico = await NovoPerfil(db, TipoPerfilAcesso.TecnicoSeguranca);
        var t1 = await NovoUsuario(db, "Tecnico1");
        db.UsuariosPerfilObra.Add(new UsuarioPerfilObra { UsuarioId = t1.Id, PerfilAcessoId = tecnico.Id, ObraId = sul.Id });
        await db.SaveChangesAsync();
        await Salvar(db, t1.Id, sul.Id, presenca: true, boletim: false, ocorrencia: false); // ajuste manual

        var criados = await new SugerirDestinatariosPorPerfilCommandHandler(db).Handle(new SugerirDestinatariosPorPerfilCommand(), CancellationToken.None);

        Assert.Equal(0, criados);
        var d = await db.DestinatariosRelatorio.SingleAsync();
        Assert.Equal((true, false, false), (d.ListaPresenca, d.BoletimSemanal, d.Ocorrencia));
    }
}

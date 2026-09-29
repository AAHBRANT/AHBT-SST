using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Assinatura.Commands;
using AAHBRANT.SST.Application.Assinatura.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Infrastructure.Tests.Assinatura;

public class AdministracaoDispositivosAgenteTests
{
    private static async Task<(Infrastructure.Persistencia.SstDbContext Db, Obra Obra, Obra Outra)> Semear(string nome)
    {
        var db = SstDbContextInMemoryTests.CriarContexto(nome);
        var obra = new Obra { Codigo = "OBR-A", Nome = "Obra A" };
        var outra = new Obra { Codigo = "OBR-B", Nome = "Obra B" };
        db.Obras.AddRange(obra, outra);
        await db.SaveChangesAsync();
        return (db, obra, outra);
    }

    [Fact]
    public async Task Listar_DevolveSoOsAtivosComNomeDaObra_FiltrandoPorObra()
    {
        var (db, obra, outra) = await Semear(nameof(Listar_DevolveSoOsAtivosComNomeDaObra_FiltrandoPorObra));
        using var _ = db;
        var hasher = new SegredoDispositivoHasherService();
        var registrar = new RegistrarDispositivoAgenteCommandHandler(db, hasher);
        var a1 = await registrar.Handle(new RegistrarDispositivoAgenteCommand(obra.Id, "PC Portaria"), default);
        await registrar.Handle(new RegistrarDispositivoAgenteCommand(outra.Id, "PC Escritório"), default);
        var revogado = await registrar.Handle(new RegistrarDispositivoAgenteCommand(obra.Id, "PC Antigo"), default);
        await new RevogarDispositivoAgenteCommandHandler(db).Handle(new RevogarDispositivoAgenteCommand(revogado.DispositivoId), default);
        var lista = new ListarDispositivosAgenteQueryHandler(db);

        var todos = await lista.Handle(new ListarDispositivosAgenteQuery(), default);
        var daObraA = await lista.Handle(new ListarDispositivosAgenteQuery(obra.Id), default);

        Assert.Equal(new[] { "PC Portaria", "PC Escritório" }, todos.Select(d => d.Nome));
        Assert.Equal(new[] { a1.DispositivoId }, daObraA.Select(d => d.Id));
        Assert.Equal("Obra A", daObraA.Single().ObraNome);
    }

    // O agente revogado (PC perdido ou trocado) precisa parar de valer na hora: nem sincroniza
    // templates nem assina por digital, porque o autenticador não o encontra mais.
    [Fact]
    public async Task Revogar_FazOSegredoDeixarDeValer()
    {
        var (db, obra, _) = await Semear(nameof(Revogar_FazOSegredoDeixarDeValer));
        using var _ = db;
        var hasher = new SegredoDispositivoHasherService();
        var registro = await new RegistrarDispositivoAgenteCommandHandler(db, hasher)
            .Handle(new RegistrarDispositivoAgenteCommand(obra.Id, "PC Portaria"), default);
        var autenticador = new DispositivoAgenteAutenticador(db, hasher);
        Assert.NotNull(await autenticador.ValidarAsync(registro.DispositivoId, registro.Segredo, default));

        await new RevogarDispositivoAgenteCommandHandler(db).Handle(new RevogarDispositivoAgenteCommand(registro.DispositivoId), default);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            autenticador.ValidarAsync(registro.DispositivoId, registro.Segredo, default));
        // Exclusão lógica: o registro continua no banco, para auditoria.
        Assert.False((await db.DispositivosAgenteBiometrico.IgnoreQueryFilters().SingleAsync()).Ativo);
    }

    [Fact]
    public async Task Revogar_DispositivoInexistente_LancaKeyNotFound()
    {
        var (db, _, _) = await Semear(nameof(Revogar_DispositivoInexistente_LancaKeyNotFound));
        using var _ = db;

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new RevogarDispositivoAgenteCommandHandler(db).Handle(new RevogarDispositivoAgenteCommand(Guid.NewGuid()), default));
    }

    // Sem o Kind UTC o JSON sai sem "Z" e a tela de administração mostra o horário (e o "conectado") errado.
    [Fact]
    public async Task Listar_DevolveDatasMarcadasComoUtc()
    {
        var (db, obra, _) = await Semear(nameof(Listar_DevolveDatasMarcadasComoUtc));
        using var _ = db;
        var registro = await new RegistrarDispositivoAgenteCommandHandler(db, new SegredoDispositivoHasherService())
            .Handle(new RegistrarDispositivoAgenteCommand(obra.Id, "PC Portaria"), default);
        (await db.DispositivosAgenteBiometrico.SingleAsync(d => d.Id == registro.DispositivoId)).UltimaSincronizacaoEm = new DateTime(2026, 9, 29, 13, 2, 0);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var dto = (await new ListarDispositivosAgenteQueryHandler(db).Handle(new ListarDispositivosAgenteQuery(), default)).Single();

        Assert.Equal(DateTimeKind.Utc, dto.UltimaSincronizacaoEm!.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, dto.RegistradoEm.Kind);
        Assert.Equal(new DateTime(2026, 9, 29, 13, 2, 0, DateTimeKind.Utc), dto.UltimaSincronizacaoEm);
    }
}

using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Funcoes.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Funcoes;

public class DefinirMatrizEpiFuncaoCommandHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options);
    }

    private static async Task<(Funcao Funcao, CatalogoEpi EpiA, CatalogoEpi EpiB, CatalogoEpi EpiC)> SemearAsync(IAppDbContext db)
    {
        var funcao = new Funcao { Nome = "Soldador" };
        var epiA = new CatalogoEpi { Nome = "Capacete", VidaUtilEmMeses = 12 };
        var epiB = new CatalogoEpi { Nome = "Luva", VidaUtilEmMeses = 6 };
        var epiC = new CatalogoEpi { Nome = "Óculos", VidaUtilEmMeses = 12 };

        db.Funcoes.Add(funcao);
        db.CatalogoEpis.AddRange(epiA, epiB, epiC);
        await db.SaveChangesAsync();

        return (funcao, epiA, epiB, epiC);
    }

    [Fact]
    public async Task Handle_FuncaoSemVinculos_AdicionaTodosOsEpisInformados()
    {
        var db = CriarDb(nameof(Handle_FuncaoSemVinculos_AdicionaTodosOsEpisInformados));
        var (funcao, epiA, epiB, _) = await SemearAsync(db);
        var handler = new DefinirMatrizEpiFuncaoCommandHandler(db);

        await handler.Handle(new DefinirMatrizEpiFuncaoCommand(funcao.Id, new List<Guid> { epiA.Id, epiB.Id }), default);

        var vinculos = await db.MatrizEpiFuncoes.Where(m => m.FuncaoId == funcao.Id).ToListAsync();
        Assert.Equal(2, vinculos.Count);
        Assert.Contains(vinculos, v => v.CatalogoEpiId == epiA.Id);
        Assert.Contains(vinculos, v => v.CatalogoEpiId == epiB.Id);
    }

    [Fact]
    public async Task Handle_RemoveEpiDaLista_DesativaVinculoExistente()
    {
        var db = CriarDb(nameof(Handle_RemoveEpiDaLista_DesativaVinculoExistente));
        var (funcao, epiA, epiB, _) = await SemearAsync(db);
        var handler = new DefinirMatrizEpiFuncaoCommandHandler(db);
        await handler.Handle(new DefinirMatrizEpiFuncaoCommand(funcao.Id, new List<Guid> { epiA.Id, epiB.Id }), default);

        await handler.Handle(new DefinirMatrizEpiFuncaoCommand(funcao.Id, new List<Guid> { epiA.Id }), default);

        var vinculos = await db.MatrizEpiFuncoes.IgnoreQueryFilters().Where(m => m.FuncaoId == funcao.Id).ToListAsync();
        Assert.Equal(2, vinculos.Count);
        Assert.True(vinculos.Single(v => v.CatalogoEpiId == epiA.Id).Ativo);
        Assert.False(vinculos.Single(v => v.CatalogoEpiId == epiB.Id).Ativo);
    }

    [Fact]
    public async Task Handle_ReenviaEpiRemovidoAnteriormente_ReativaVinculoEmVezDeDuplicar()
    {
        var db = CriarDb(nameof(Handle_ReenviaEpiRemovidoAnteriormente_ReativaVinculoEmVezDeDuplicar));
        var (funcao, epiA, epiB, _) = await SemearAsync(db);
        var handler = new DefinirMatrizEpiFuncaoCommandHandler(db);
        await handler.Handle(new DefinirMatrizEpiFuncaoCommand(funcao.Id, new List<Guid> { epiA.Id, epiB.Id }), default);
        await handler.Handle(new DefinirMatrizEpiFuncaoCommand(funcao.Id, new List<Guid> { epiA.Id }), default);

        await handler.Handle(new DefinirMatrizEpiFuncaoCommand(funcao.Id, new List<Guid> { epiA.Id, epiB.Id }), default);

        var vinculos = await db.MatrizEpiFuncoes.Where(m => m.FuncaoId == funcao.Id).ToListAsync();
        Assert.Equal(2, vinculos.Count);
        Assert.All(vinculos, v => Assert.True(v.Ativo));
    }

    [Fact]
    public async Task Handle_ReenviaMesmaLista_EhIdempotente()
    {
        var db = CriarDb(nameof(Handle_ReenviaMesmaLista_EhIdempotente));
        var (funcao, epiA, epiB, _) = await SemearAsync(db);
        var handler = new DefinirMatrizEpiFuncaoCommandHandler(db);
        await handler.Handle(new DefinirMatrizEpiFuncaoCommand(funcao.Id, new List<Guid> { epiA.Id, epiB.Id }), default);

        await handler.Handle(new DefinirMatrizEpiFuncaoCommand(funcao.Id, new List<Guid> { epiA.Id, epiB.Id }), default);

        var vinculos = await db.MatrizEpiFuncoes.Where(m => m.FuncaoId == funcao.Id).ToListAsync();
        Assert.Equal(2, vinculos.Count);
    }

    [Fact]
    public async Task Handle_FuncaoInexistente_LancaKeyNotFoundException()
    {
        var db = CriarDb(nameof(Handle_FuncaoInexistente_LancaKeyNotFoundException));
        var handler = new DefinirMatrizEpiFuncaoCommandHandler(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new DefinirMatrizEpiFuncaoCommand(Guid.NewGuid(), new List<Guid>()), default));
    }
}

using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Funcoes.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Funcoes;

public class DefinirMatrizUniformeFuncaoCommandHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private static async Task<(Funcao Funcao, CatalogoUniforme PecaA, CatalogoUniforme PecaB, CatalogoUniforme PecaC)> SemearAsync(IAppDbContext db)
    {
        var funcao = new Funcao { Nome = "Pedreiro" };
        var pecaA = new CatalogoUniforme { Nome = "Camisa" };
        var pecaB = new CatalogoUniforme { Nome = "Calça" };
        var pecaC = new CatalogoUniforme { Nome = "Bota" };

        db.Funcoes.Add(funcao);
        db.CatalogoUniformes.AddRange(pecaA, pecaB, pecaC);
        await db.SaveChangesAsync();

        return (funcao, pecaA, pecaB, pecaC);
    }

    [Fact]
    public async Task Handle_FuncaoSemVinculos_AdicionaTodasAsPecasInformadas()
    {
        var db = CriarDb(nameof(Handle_FuncaoSemVinculos_AdicionaTodasAsPecasInformadas));
        var (funcao, pecaA, pecaB, _) = await SemearAsync(db);
        var handler = new DefinirMatrizUniformeFuncaoCommandHandler(db);

        await handler.Handle(new DefinirMatrizUniformeFuncaoCommand(funcao.Id, new List<Guid> { pecaA.Id, pecaB.Id }), default);

        var vinculos = await db.MatrizUniformeFuncoes.Where(m => m.FuncaoId == funcao.Id).ToListAsync();
        Assert.Equal(2, vinculos.Count);
        Assert.Contains(vinculos, v => v.CatalogoUniformeId == pecaA.Id);
        Assert.Contains(vinculos, v => v.CatalogoUniformeId == pecaB.Id);
    }

    [Fact]
    public async Task Handle_RemovePecaDaLista_DesativaVinculoExistente()
    {
        var db = CriarDb(nameof(Handle_RemovePecaDaLista_DesativaVinculoExistente));
        var (funcao, pecaA, pecaB, _) = await SemearAsync(db);
        var handler = new DefinirMatrizUniformeFuncaoCommandHandler(db);
        await handler.Handle(new DefinirMatrizUniformeFuncaoCommand(funcao.Id, new List<Guid> { pecaA.Id, pecaB.Id }), default);

        await handler.Handle(new DefinirMatrizUniformeFuncaoCommand(funcao.Id, new List<Guid> { pecaA.Id }), default);

        var vinculos = await db.MatrizUniformeFuncoes.IgnoreQueryFilters().Where(m => m.FuncaoId == funcao.Id).ToListAsync();
        Assert.Equal(2, vinculos.Count);
        Assert.True(vinculos.Single(v => v.CatalogoUniformeId == pecaA.Id).Ativo);
        Assert.False(vinculos.Single(v => v.CatalogoUniformeId == pecaB.Id).Ativo);
    }

    [Fact]
    public async Task Handle_ReenviaPecaRemovidaAnteriormente_ReativaVinculoEmVezDeDuplicar()
    {
        var db = CriarDb(nameof(Handle_ReenviaPecaRemovidaAnteriormente_ReativaVinculoEmVezDeDuplicar));
        var (funcao, pecaA, pecaB, _) = await SemearAsync(db);
        var handler = new DefinirMatrizUniformeFuncaoCommandHandler(db);
        await handler.Handle(new DefinirMatrizUniformeFuncaoCommand(funcao.Id, new List<Guid> { pecaA.Id, pecaB.Id }), default);
        await handler.Handle(new DefinirMatrizUniformeFuncaoCommand(funcao.Id, new List<Guid> { pecaA.Id }), default);

        await handler.Handle(new DefinirMatrizUniformeFuncaoCommand(funcao.Id, new List<Guid> { pecaA.Id, pecaB.Id }), default);

        var vinculos = await db.MatrizUniformeFuncoes.Where(m => m.FuncaoId == funcao.Id).ToListAsync();
        Assert.Equal(2, vinculos.Count);
        Assert.All(vinculos, v => Assert.True(v.Ativo));
    }

    [Fact]
    public async Task Handle_FuncaoInexistente_LancaKeyNotFoundException()
    {
        var db = CriarDb(nameof(Handle_FuncaoInexistente_LancaKeyNotFoundException));
        var handler = new DefinirMatrizUniformeFuncaoCommandHandler(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new DefinirMatrizUniformeFuncaoCommand(Guid.NewGuid(), new List<Guid>()), default));
    }
}

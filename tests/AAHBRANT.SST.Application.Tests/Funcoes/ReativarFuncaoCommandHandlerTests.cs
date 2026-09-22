using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Funcoes.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Funcoes;

public class ReativarFuncaoCommandHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_FuncaoInativa_VoltaAAtiva()
    {
        var db = CriarDb(nameof(Handle_FuncaoInativa_VoltaAAtiva));
        var funcao = new Funcao { Nome = "Técnico de Segurança do Trabalho", CboCodigo = "3516-05", Ativo = false };
        db.Funcoes.Add(funcao);
        await db.SaveChangesAsync();

        var handler = new ReativarFuncaoCommandHandler(db);
        await handler.Handle(new ReativarFuncaoCommand(funcao.Id), default);

        var recarregada = await db.Funcoes.IgnoreQueryFilters().FirstAsync(f => f.Id == funcao.Id);
        Assert.True(recarregada.Ativo);
    }

    [Fact]
    public async Task Handle_FuncaoInexistente_LancaKeyNotFoundException()
    {
        var db = CriarDb(nameof(Handle_FuncaoInexistente_LancaKeyNotFoundException));

        var handler = new ReativarFuncaoCommandHandler(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => handler.Handle(new ReativarFuncaoCommand(Guid.NewGuid()), default));
    }
}

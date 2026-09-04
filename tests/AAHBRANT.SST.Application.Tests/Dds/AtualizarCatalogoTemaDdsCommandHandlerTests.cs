using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Dds.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Dds;

public class AtualizarCatalogoTemaDdsCommandHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_TemaExistente_AtualizaNomeEDescricao()
    {
        var db = CriarDb(nameof(Handle_TemaExistente_AtualizaNomeEDescricao));
        var tema = new CatalogoTemaDds { Nome = "Nome antigo", Descricao = "Descrição antiga" };
        db.CatalogosTemaDds.Add(tema);
        await db.SaveChangesAsync();
        var handler = new AtualizarCatalogoTemaDdsCommandHandler(db);

        await handler.Handle(new AtualizarCatalogoTemaDdsCommand(tema.Id, "Outubro Amarelo", "Prevenção ao suicídio"), default);

        var atualizado = await db.CatalogosTemaDds.FirstAsync(x => x.Id == tema.Id);
        Assert.Equal("Outubro Amarelo", atualizado.Nome);
        Assert.Equal("Prevenção ao suicídio", atualizado.Descricao);
    }

    [Fact]
    public async Task Handle_TemaInexistente_LancaKeyNotFoundException()
    {
        var db = CriarDb(nameof(Handle_TemaInexistente_LancaKeyNotFoundException));
        var handler = new AtualizarCatalogoTemaDdsCommandHandler(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new AtualizarCatalogoTemaDdsCommand(Guid.NewGuid(), "Nome", null), default));
    }
}

using AAHBRANT.SST.Application.Alojamentos.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Alojamentos;

public class CriarAlojamentoCommandHandlerTests
{
    private static SstDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nomeBanco).Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_ObraValida_CriaAlojamentoAtivo()
    {
        var db = CriarDb(nameof(Handle_ObraValida_CriaAlojamentoAtivo));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        var handler = new CriarAlojamentoCommandHandler(db);
        var id = await handler.Handle(new CriarAlojamentoCommand("Alojamento 01", "Rua X, 100", obra.Id), default);

        var alojamento = await db.Alojamentos.FirstAsync(a => a.Id == id);
        Assert.Equal("Alojamento 01", alojamento.Nome);
        Assert.True(alojamento.Ativo);
    }

    [Fact]
    public async Task Handle_ObraInexistente_LancaKeyNotFoundException()
    {
        var db = CriarDb(nameof(Handle_ObraInexistente_LancaKeyNotFoundException));
        var handler = new CriarAlojamentoCommandHandler(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new CriarAlojamentoCommand("Alojamento 01", null, Guid.NewGuid()), default));
    }
}

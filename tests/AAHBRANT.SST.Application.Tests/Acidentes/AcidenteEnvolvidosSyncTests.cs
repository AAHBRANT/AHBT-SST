using AAHBRANT.SST.Application.Acidentes;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Acidentes;

public class AcidenteEnvolvidosSyncTests
{
    [Fact]
    public void Resolver_ListaTemPrecedencia_SemDuplicatasEVazios()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var ids = AcidenteEnvolvidosSync.Resolver(new[] { a, b, a, Guid.Empty }, Guid.NewGuid());
        Assert.Equal(new[] { a, b }, ids);
    }

    [Fact]
    public void Resolver_SemLista_UsaTrabalhadorIdLegado()
    {
        var legado = Guid.NewGuid();
        Assert.Equal(new[] { legado }, AcidenteEnvolvidosSync.Resolver(null, legado));
        Assert.Empty(AcidenteEnvolvidosSync.Resolver(null, null));
    }

    [Fact]
    public async Task Sincronizar_PrimeiroViraPrincipal_ERemoveQuemSaiu()
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nameof(Sincronizar_PrimeiroViraPrincipal_ERemoveQuemSaiu)).Options;
        var db = new SstDbContext(options, new CurrentUserService());
        var acidente = new Acidente { Local = "x", Descricao = "y" };
        db.Acidentes.Add(acidente);
        await db.SaveChangesAsync();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();

        await AcidenteEnvolvidosSync.SincronizarAsync(db, acidente, new List<Guid> { a, b }, default);
        await db.SaveChangesAsync();
        Assert.Equal(a, acidente.TrabalhadorId);

        await AcidenteEnvolvidosSync.SincronizarAsync(db, acidente, new List<Guid> { b, c }, default);
        await db.SaveChangesAsync();

        Assert.Equal(b, acidente.TrabalhadorId);
        var ativos = await db.AcidentesEnvolvidos.Select(e => e.TrabalhadorId).ToListAsync();
        Assert.Equal(new[] { b, c }.OrderBy(x => x), ativos.OrderBy(x => x));
    }
}

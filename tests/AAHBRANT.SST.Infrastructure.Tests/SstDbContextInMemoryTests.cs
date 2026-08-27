using AAHBRANT.SST.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Infrastructure.Tests;

public class SstDbContextInMemoryTests
{
    public static SstDbContext CriarContexto(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options);
    }

    [Fact]
    public void DeveCriarContextoInMemorySemErro()
    {
        using var db = CriarContexto(nameof(DeveCriarContextoInMemorySemErro));
        Assert.NotNull(db);
    }
}

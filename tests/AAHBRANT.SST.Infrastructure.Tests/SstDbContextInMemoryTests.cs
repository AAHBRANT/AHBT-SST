using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Infrastructure.Tests;

public class SstDbContextInMemoryTests
{
    public static SstDbContext CriarContexto(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public void DeveCriarContextoInMemorySemErro()
    {
        using var db = CriarContexto(nameof(DeveCriarContextoInMemorySemErro));
        Assert.NotNull(db);
    }
}

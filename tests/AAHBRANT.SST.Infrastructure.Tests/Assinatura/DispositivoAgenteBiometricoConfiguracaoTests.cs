using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Infrastructure.Tests.Assinatura;

public class DispositivoAgenteBiometricoConfiguracaoTests
{
    [Fact]
    public async Task DevePersistirEDesativarPorQueryFilter()
    {
        using var db = SstDbContextInMemoryTests.CriarContexto(nameof(DevePersistirEDesativarPorQueryFilter));

        var dispositivo = new DispositivoAgenteBiometrico
        {
            ObraId = Guid.NewGuid(),
            Nome = "Quiosque Portaria",
            SegredoHash = "hash-fake",
        };

        db.DispositivosAgenteBiometrico.Add(dispositivo);
        await db.SaveChangesAsync();

        var encontrado = await db.DispositivosAgenteBiometrico.FindAsync(dispositivo.Id);
        Assert.NotNull(encontrado);

        encontrado!.Ativo = false;
        await db.SaveChangesAsync();

        var aposDesativar = await db.DispositivosAgenteBiometrico
            .FirstOrDefaultAsync(d => d.Id == dispositivo.Id);
        Assert.Null(aposDesativar);
    }
}

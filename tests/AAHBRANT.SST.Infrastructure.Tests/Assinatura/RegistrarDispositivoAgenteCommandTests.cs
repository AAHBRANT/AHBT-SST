using AAHBRANT.SST.Application.Assinatura.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Seguranca;
using AAHBRANT.SST.Infrastructure.Tests;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Infrastructure.Tests.Assinatura;

public class RegistrarDispositivoAgenteCommandTests
{
    [Fact]
    public async Task Handle_ComObraExistente_DeveCriarDispositivoERetornarSegredoEmClaro()
    {
        using var db = SstDbContextInMemoryTests.CriarContexto(nameof(Handle_ComObraExistente_DeveCriarDispositivoERetornarSegredoEmClaro));
        var obra = new Obra { Codigo = "OBR-006", Nome = "Obra Teste 6" };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        var handler = new RegistrarDispositivoAgenteCommandHandler(db, new SegredoDispositivoHasherService());
        var comando = new RegistrarDispositivoAgenteCommand(obra.Id, "Totem Portaria");

        var segredo = await handler.Handle(comando, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(segredo));
        var dispositivo = await db.DispositivosAgenteBiometrico.FirstOrDefaultAsync(d => d.ObraId == obra.Id);
        Assert.NotNull(dispositivo);
        Assert.NotEqual(segredo, dispositivo!.SegredoHash);
    }

    [Fact]
    public async Task Handle_ComObraInexistente_DeveLancarKeyNotFoundException()
    {
        using var db = SstDbContextInMemoryTests.CriarContexto(nameof(Handle_ComObraInexistente_DeveLancarKeyNotFoundException));
        var handler = new RegistrarDispositivoAgenteCommandHandler(db, new SegredoDispositivoHasherService());
        var comando = new RegistrarDispositivoAgenteCommand(Guid.NewGuid(), "Totem Portaria");

        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(comando, CancellationToken.None));
    }
}

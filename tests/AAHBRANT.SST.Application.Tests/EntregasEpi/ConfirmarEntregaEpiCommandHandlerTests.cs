using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.EntregasEpi.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.EntregasEpi;

public class ConfirmarEntregaEpiCommandHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_EntregaPendente_MarcaConfirmadaEPreenchDataConfirmacao()
    {
        var db = CriarDb(nameof(Handle_EntregaPendente_MarcaConfirmadaEPreenchDataConfirmacao));
        var entrega = new EntregaEpi
        {
            TrabalhadorId = Guid.NewGuid(),
            CatalogoEpiId = Guid.NewGuid(),
            DataEntrega = DateTime.UtcNow,
            Quantidade = 1,
            Confirmada = false,
        };
        db.EntregasEpi.Add(entrega);
        await db.SaveChangesAsync();
        var handler = new ConfirmarEntregaEpiCommandHandler(db);

        await handler.Handle(new ConfirmarEntregaEpiCommand(entrega.Id), default);

        var atualizada = await db.EntregasEpi.FirstAsync(x => x.Id == entrega.Id);
        Assert.True(atualizada.Confirmada);
        Assert.NotNull(atualizada.DataConfirmacao);
    }

    [Fact]
    public async Task Handle_EntregaJaConfirmada_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Handle_EntregaJaConfirmada_LancaInvalidOperationException));
        var entrega = new EntregaEpi
        {
            TrabalhadorId = Guid.NewGuid(),
            CatalogoEpiId = Guid.NewGuid(),
            DataEntrega = DateTime.UtcNow,
            Quantidade = 1,
            Confirmada = true,
        };
        db.EntregasEpi.Add(entrega);
        await db.SaveChangesAsync();
        var handler = new ConfirmarEntregaEpiCommandHandler(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new ConfirmarEntregaEpiCommand(entrega.Id), default));
    }

    [Fact]
    public async Task Handle_EntregaInexistente_LancaKeyNotFoundException()
    {
        var db = CriarDb(nameof(Handle_EntregaInexistente_LancaKeyNotFoundException));
        var handler = new ConfirmarEntregaEpiCommandHandler(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new ConfirmarEntregaEpiCommand(Guid.NewGuid()), default));
    }
}

using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Seguranca;
using AAHBRANT.SST.Infrastructure.Tests;

namespace AAHBRANT.SST.Infrastructure.Tests.Assinatura;

public class DispositivoAgenteAutenticadorTests
{
    private static async Task<(Persistencia.SstDbContext Db, DispositivoAgenteBiometrico Dispositivo, string Segredo)> PrepararAsync(string nomeBanco)
    {
        var db = SstDbContextInMemoryTests.CriarContexto(nomeBanco);
        var obra = new Obra { Codigo = "OBR-003", Nome = "Obra Teste 3" };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        var segredo = SegredoDispositivoHasher.GerarSegredo();
        var dispositivo = new DispositivoAgenteBiometrico
        {
            ObraId = obra.Id,
            Nome = "Totem 1",
            SegredoHash = SegredoDispositivoHasher.GerarHash(segredo),
        };
        db.DispositivosAgenteBiometrico.Add(dispositivo);
        await db.SaveChangesAsync();

        return (db, dispositivo, segredo);
    }

    [Fact]
    public async Task ValidarAsync_ComSegredoCorreto_DeveRetornarDispositivo()
    {
        var (db, dispositivo, segredo) = await PrepararAsync(nameof(ValidarAsync_ComSegredoCorreto_DeveRetornarDispositivo));
        var autenticador = new DispositivoAgenteAutenticador(db, new SegredoDispositivoHasherService());

        var resultado = await autenticador.ValidarAsync(dispositivo.Id, segredo, CancellationToken.None);

        Assert.Equal(dispositivo.Id, resultado.Id);
    }

    [Fact]
    public async Task ValidarAsync_ComSegredoErrado_DeveLancarInvalidOperationException()
    {
        var (db, dispositivo, _) = await PrepararAsync(nameof(ValidarAsync_ComSegredoErrado_DeveLancarInvalidOperationException));
        var autenticador = new DispositivoAgenteAutenticador(db, new SegredoDispositivoHasherService());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            autenticador.ValidarAsync(dispositivo.Id, "segredo-errado", CancellationToken.None));
    }

    [Fact]
    public async Task ValidarAsync_ComDispositivoInexistente_DeveLancarInvalidOperationException()
    {
        var db = SstDbContextInMemoryTests.CriarContexto(nameof(ValidarAsync_ComDispositivoInexistente_DeveLancarInvalidOperationException));
        var autenticador = new DispositivoAgenteAutenticador(db, new SegredoDispositivoHasherService());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            autenticador.ValidarAsync(Guid.NewGuid(), "qualquer-coisa", CancellationToken.None));
    }
}

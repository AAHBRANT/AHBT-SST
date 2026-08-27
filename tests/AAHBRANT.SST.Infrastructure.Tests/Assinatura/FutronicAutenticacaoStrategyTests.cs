using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Assinatura;
using AAHBRANT.SST.Infrastructure.Seguranca;
using AAHBRANT.SST.Infrastructure.Tests;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Tests.Assinatura;

public class FutronicAutenticacaoStrategyTests
{
    private static async Task<(Persistencia.SstDbContext Db, DispositivoAgenteBiometrico Dispositivo, string Segredo, Trabalhador Trabalhador)> PrepararAsync(string nomeBanco, bool habilitarBiometria = true, bool termoAceito = true)
    {
        var db = SstDbContextInMemoryTests.CriarContexto(nomeBanco);
        var obra = new Obra
        {
            Codigo = "OBR-004",
            Nome = "Obra Teste 4",
            MetodosAutenticacaoHabilitados = habilitarBiometria ? MetodoAutenticacaoObra.Biometria : MetodoAutenticacaoObra.Nenhum,
        };
        db.Obras.Add(obra);

        var trabalhador = new Trabalhador
        {
            ObraId = obra.Id,
            Nome = "Ciclano",
            Matricula = "M-002",
            Cpf = "98765432100",
            TermoAceiteAssinaturaEletronicaEm = termoAceito ? DateTime.UtcNow : null,
            ConsentimentoBiometriaEm = termoAceito ? DateTime.UtcNow : null,
        };
        db.Trabalhadores.Add(trabalhador);

        var segredo = SegredoDispositivoHasher.GerarSegredo();
        var dispositivo = new DispositivoAgenteBiometrico
        {
            ObraId = obra.Id,
            Nome = "Totem 2",
            SegredoHash = SegredoDispositivoHasher.GerarHash(segredo),
        };
        db.DispositivosAgenteBiometrico.Add(dispositivo);

        await db.SaveChangesAsync();
        return (db, dispositivo, segredo, trabalhador);
    }

    private static FutronicAutenticacaoStrategy CriarStrategy(Persistencia.SstDbContext db, double limiar = 50)
    {
        var autenticador = new DispositivoAgenteAutenticador(db, new SegredoDispositivoHasherService());
        var options = Options.Create(new AssinaturaOptions { LimiarConfiancaBiometriaLocal = limiar });
        return new FutronicAutenticacaoStrategy(db, autenticador, options);
    }

    [Fact]
    public async Task AutenticarPorMatchLocalAsync_ComScoreAcimaDoLimiar_DeveRetornarResultado()
    {
        var (db, dispositivo, segredo, trabalhador) = await PrepararAsync(nameof(AutenticarPorMatchLocalAsync_ComScoreAcimaDoLimiar_DeveRetornarResultado));
        var strategy = CriarStrategy(db);

        var resultado = await strategy.AutenticarPorMatchLocalAsync(dispositivo.Id, segredo, trabalhador.Id, 80, CancellationToken.None);

        Assert.Equal(trabalhador.Id, resultado.TrabalhadorId);
        Assert.Equal(MetodoAutenticacaoAssinatura.Biometria, resultado.Metodo);
    }

    [Fact]
    public async Task AutenticarPorMatchLocalAsync_ComScoreAbaixoDoLimiar_DeveLancarInvalidOperationException()
    {
        var (db, dispositivo, segredo, trabalhador) = await PrepararAsync(nameof(AutenticarPorMatchLocalAsync_ComScoreAbaixoDoLimiar_DeveLancarInvalidOperationException));
        var strategy = CriarStrategy(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            strategy.AutenticarPorMatchLocalAsync(dispositivo.Id, segredo, trabalhador.Id, 10, CancellationToken.None));
    }

    [Fact]
    public async Task AutenticarPorMatchLocalAsync_ComObraSemMetodoHabilitado_DeveLancarInvalidOperationException()
    {
        var (db, dispositivo, segredo, trabalhador) = await PrepararAsync(nameof(AutenticarPorMatchLocalAsync_ComObraSemMetodoHabilitado_DeveLancarInvalidOperationException), habilitarBiometria: false);
        var strategy = CriarStrategy(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            strategy.AutenticarPorMatchLocalAsync(dispositivo.Id, segredo, trabalhador.Id, 80, CancellationToken.None));
    }

    [Fact]
    public async Task AutenticarPorMatchLocalAsync_SemTermoOuConsentimento_DeveLancarInvalidOperationException()
    {
        var (db, dispositivo, segredo, trabalhador) = await PrepararAsync(nameof(AutenticarPorMatchLocalAsync_SemTermoOuConsentimento_DeveLancarInvalidOperationException), termoAceito: false);
        var strategy = CriarStrategy(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            strategy.AutenticarPorMatchLocalAsync(dispositivo.Id, segredo, trabalhador.Id, 80, CancellationToken.None));
    }

    [Fact]
    public async Task AutenticarPorMatchLocalAsync_ComTrabalhadorDeOutraObra_DeveLancarKeyNotFoundException()
    {
        var (db, dispositivo, segredo, _) = await PrepararAsync(nameof(AutenticarPorMatchLocalAsync_ComTrabalhadorDeOutraObra_DeveLancarKeyNotFoundException));

        var outraObra = new Obra { Codigo = "OBR-005", Nome = "Outra Obra", MetodosAutenticacaoHabilitados = MetodoAutenticacaoObra.Biometria };
        db.Obras.Add(outraObra);
        var trabalhadorDeOutraObra = new Trabalhador
        {
            ObraId = outraObra.Id,
            Nome = "Beltrano",
            Matricula = "M-003",
            Cpf = "11122233344",
            TermoAceiteAssinaturaEletronicaEm = DateTime.UtcNow,
            ConsentimentoBiometriaEm = DateTime.UtcNow,
        };
        db.Trabalhadores.Add(trabalhadorDeOutraObra);
        await db.SaveChangesAsync();

        var strategy = CriarStrategy(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            strategy.AutenticarPorMatchLocalAsync(dispositivo.Id, segredo, trabalhadorDeOutraObra.Id, 80, CancellationToken.None));
    }
}

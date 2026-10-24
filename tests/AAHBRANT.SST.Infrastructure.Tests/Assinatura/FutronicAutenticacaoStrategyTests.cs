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

    private static FutronicAutenticacaoStrategy CriarStrategy(Persistencia.SstDbContext db, double limiar = 50, bool exigirAssinada = false)
    {
        var autenticador = new DispositivoAgenteAutenticador(db, new SegredoDispositivoHasherService());
        var options = Options.Create(new AssinaturaOptions { LimiarConfiancaBiometriaLocal = limiar, ExigirLeituraAssinadaBiometria = exigirAssinada });
        return new FutronicAutenticacaoStrategy(db, autenticador, options);
    }

    [Fact]
    public async Task AutenticarPorMatchLocalAsync_ComScoreAcimaDoLimiar_DeveRetornarResultado()
    {
        var (db, dispositivo, segredo, trabalhador) = await PrepararAsync(nameof(AutenticarPorMatchLocalAsync_ComScoreAcimaDoLimiar_DeveRetornarResultado));
        var strategy = CriarStrategy(db);

        var resultado = await strategy.AutenticarPorMatchLocalAsync(dispositivo.Id, segredo, trabalhador.Id, 80, null, CancellationToken.None);

        Assert.Equal(trabalhador.Id, resultado.TrabalhadorId);
        Assert.Equal(MetodoAutenticacaoAssinatura.Biometria, resultado.Metodo);
    }

    [Fact]
    public async Task AutenticarPorMatchLocalAsync_ComScoreAbaixoDoLimiar_DeveLancarInvalidOperationException()
    {
        var (db, dispositivo, segredo, trabalhador) = await PrepararAsync(nameof(AutenticarPorMatchLocalAsync_ComScoreAbaixoDoLimiar_DeveLancarInvalidOperationException));
        var strategy = CriarStrategy(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            strategy.AutenticarPorMatchLocalAsync(dispositivo.Id, segredo, trabalhador.Id, 10, null, CancellationToken.None));
    }

    [Fact]
    public async Task AutenticarPorMatchLocalAsync_ComObraSemMetodoHabilitado_DeveLancarInvalidOperationException()
    {
        var (db, dispositivo, segredo, trabalhador) = await PrepararAsync(nameof(AutenticarPorMatchLocalAsync_ComObraSemMetodoHabilitado_DeveLancarInvalidOperationException), habilitarBiometria: false);
        var strategy = CriarStrategy(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            strategy.AutenticarPorMatchLocalAsync(dispositivo.Id, segredo, trabalhador.Id, 80, null, CancellationToken.None));
    }

    [Fact]
    public async Task AutenticarPorMatchLocalAsync_SemTermoOuConsentimento_DeveLancarInvalidOperationException()
    {
        var (db, dispositivo, segredo, trabalhador) = await PrepararAsync(nameof(AutenticarPorMatchLocalAsync_SemTermoOuConsentimento_DeveLancarInvalidOperationException), termoAceito: false);
        var strategy = CriarStrategy(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            strategy.AutenticarPorMatchLocalAsync(dispositivo.Id, segredo, trabalhador.Id, 80, null, CancellationToken.None));
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
            strategy.AutenticarPorMatchLocalAsync(dispositivo.Id, segredo, trabalhadorDeOutraObra.Id, 80, null, CancellationToken.None));
    }

    // ---- Leitura assinada pelo agente (11/10): o servidor não confia mais no trabalhador/nota do navegador ----

    private static async Task<(System.Security.Cryptography.ECDsa Chave, string Desafio)> PrepararLeituraAssinadaAsync(
        Persistencia.SstDbContext db, DispositivoAgenteBiometrico dispositivo, DateTime? expiraEm = null)
    {
        var chave = System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        dispositivo.ChavePublicaAssinatura = Convert.ToBase64String(chave.ExportSubjectPublicKeyInfo());
        var desafio = new DesafioBiometria { DispositivoId = dispositivo.Id, ExpiraEm = expiraEm ?? DateTime.UtcNow.AddMinutes(3) };
        db.DesafiosBiometria.Add(desafio);
        await db.SaveChangesAsync();
        return (chave, desafio.Id.ToString("N"));
    }

    private static LeituraBiometricaAssinada Assinar(System.Security.Cryptography.ECDsa chave, string desafio, Guid trabalhadorId, double score) =>
        new(desafio, Convert.ToBase64String(chave.SignData(
            System.Text.Encoding.UTF8.GetBytes(LeituraBiometricaAssinada.Conteudo(desafio, trabalhadorId, score)),
            System.Security.Cryptography.HashAlgorithmName.SHA256)));

    [Fact]
    public void Conteudo_FormatoFixoCompartilhadoComOAgente()
    {
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");
        Assert.Equal("abc|11111111-2222-3333-4444-555555555555|87.50", LeituraBiometricaAssinada.Conteudo("abc", id, 87.5));
    }

    [Fact]
    public async Task LeituraAssinada_Valida_AutenticaSemSegredoEConsomeODesafio()
    {
        var (db, dispositivo, _, trabalhador) = await PrepararAsync(nameof(LeituraAssinada_Valida_AutenticaSemSegredoEConsomeODesafio));
        var (chave, desafio) = await PrepararLeituraAssinadaAsync(db, dispositivo);

        var resultado = await CriarStrategy(db, exigirAssinada: true).AutenticarPorMatchLocalAsync(
            dispositivo.Id, "", trabalhador.Id, 80, Assinar(chave, desafio, trabalhador.Id, 80), CancellationToken.None);

        Assert.Equal(trabalhador.Id, resultado.TrabalhadorId);
        Assert.All(db.DesafiosBiometria, d => Assert.NotNull(d.UsadoEm));
    }

    // O ataque que motivou a mudança: trocar o trabalhador ou subir a nota depois da leitura.
    [Fact]
    public async Task LeituraAssinada_NotaOuTrabalhadorAlterados_Recusa()
    {
        var (db, dispositivo, _, trabalhador) = await PrepararAsync(nameof(LeituraAssinada_NotaOuTrabalhadorAlterados_Recusa));
        var (chave, desafio) = await PrepararLeituraAssinadaAsync(db, dispositivo);
        var leitura = Assinar(chave, desafio, trabalhador.Id, 40);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => CriarStrategy(db).AutenticarPorMatchLocalAsync(
            dispositivo.Id, "", trabalhador.Id, 100, leitura, CancellationToken.None));
        Assert.Contains("assinatura inválida", erro.Message);
    }

    [Fact]
    public async Task LeituraAssinada_AssinadaPorOutraChave_Recusa()
    {
        var (db, dispositivo, _, trabalhador) = await PrepararAsync(nameof(LeituraAssinada_AssinadaPorOutraChave_Recusa));
        var (_, desafio) = await PrepararLeituraAssinadaAsync(db, dispositivo);
        using var outra = System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);

        await Assert.ThrowsAsync<InvalidOperationException>(() => CriarStrategy(db).AutenticarPorMatchLocalAsync(
            dispositivo.Id, "", trabalhador.Id, 80, Assinar(outra, desafio, trabalhador.Id, 80), CancellationToken.None));
    }

    [Fact]
    public async Task LeituraAssinada_DesafioReutilizado_Recusa()
    {
        var (db, dispositivo, _, trabalhador) = await PrepararAsync(nameof(LeituraAssinada_DesafioReutilizado_Recusa));
        var (chave, desafio) = await PrepararLeituraAssinadaAsync(db, dispositivo);
        var leitura = Assinar(chave, desafio, trabalhador.Id, 80);
        var strategy = CriarStrategy(db);
        await strategy.AutenticarPorMatchLocalAsync(dispositivo.Id, "", trabalhador.Id, 80, leitura, CancellationToken.None);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            strategy.AutenticarPorMatchLocalAsync(dispositivo.Id, "", trabalhador.Id, 80, leitura, CancellationToken.None));
        Assert.Contains("já foi usada", erro.Message);
    }

    [Fact]
    public async Task LeituraAssinada_DesafioVencido_Recusa()
    {
        var (db, dispositivo, _, trabalhador) = await PrepararAsync(nameof(LeituraAssinada_DesafioVencido_Recusa));
        var (chave, desafio) = await PrepararLeituraAssinadaAsync(db, dispositivo, DateTime.UtcNow.AddSeconds(-1));

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => CriarStrategy(db).AutenticarPorMatchLocalAsync(
            dispositivo.Id, "", trabalhador.Id, 80, Assinar(chave, desafio, trabalhador.Id, 80), CancellationToken.None));
        Assert.Contains("expirou", erro.Message);
    }

    // Leitor que já registrou a chave não aceita mais o caminho antigo (segredo + nota sem assinatura).
    [Fact]
    public async Task SemAssinatura_LeitorComChaveRegistrada_Recusa()
    {
        var (db, dispositivo, segredo, trabalhador) = await PrepararAsync(nameof(SemAssinatura_LeitorComChaveRegistrada_Recusa));
        await PrepararLeituraAssinadaAsync(db, dispositivo);

        await Assert.ThrowsAsync<InvalidOperationException>(() => CriarStrategy(db).AutenticarPorMatchLocalAsync(
            dispositivo.Id, segredo, trabalhador.Id, 80, null, CancellationToken.None));
    }

    [Fact]
    public async Task SemAssinatura_ComExigenciaLigada_Recusa()
    {
        var (db, dispositivo, segredo, trabalhador) = await PrepararAsync(nameof(SemAssinatura_ComExigenciaLigada_Recusa));

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => CriarStrategy(db, exigirAssinada: true).AutenticarPorMatchLocalAsync(
            dispositivo.Id, segredo, trabalhador.Id, 80, null, CancellationToken.None));
        Assert.Contains("Atualize o Agente", erro.Message);
    }
}

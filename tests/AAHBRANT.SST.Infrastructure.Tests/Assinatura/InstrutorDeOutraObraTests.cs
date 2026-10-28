using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Assinatura.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Assinatura;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using AAHBRANT.SST.Infrastructure.Tests;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Tests.Assinatura;

// Instrutor de outra obra (28/10): a digital dele chega ao leitor da obra da turma e só vale para
// assinar como instrutor; qualquer outro fluxo continua restrito à obra do leitor.
public class InstrutorDeOutraObraTests
{
    private record Cenario(SstDbContext Db, DispositivoAgenteBiometrico LeitorCuia, string Segredo, Trabalhador Instrutor);

    private static async Task<Cenario> PrepararAsync(string nomeBanco, bool autorizadoOutrasObras, bool comPerfil = true)
    {
        var db = SstDbContextInMemoryTests.CriarContexto(nomeBanco);
        var cuia = new Obra { Codigo = "OBR-CUIA", Nome = "Cuiá", MetodosAutenticacaoHabilitados = MetodoAutenticacaoObra.Biometria };
        var origem = new Obra { Codigo = "OBR-ORIGEM", Nome = "Obra do instrutor", MetodosAutenticacaoHabilitados = MetodoAutenticacaoObra.Biometria };
        db.Obras.AddRange(cuia, origem);

        var instrutor = new Trabalhador
        {
            ObraId = origem.Id, Nome = "Gilton", Matricula = "M-100", Cpf = "11122233344",
            TermoAceiteAssinaturaEletronicaEm = DateTime.UtcNow, ConsentimentoBiometriaEm = DateTime.UtcNow,
        };
        db.Trabalhadores.Add(instrutor);
        if (comPerfil)
            db.PerfisInstrutor.Add(new PerfilInstrutor { TrabalhadorId = instrutor.Id, PodeAssinarEmOutrasObras = autorizadoOutrasObras });
        db.TemplatesBiometricoFutronic.Add(new TemplateBiometricoFutronic { TrabalhadorId = instrutor.Id, TemplateCriptografado = "cifrado-instrutor", CapturadoEm = DateTime.UtcNow });

        var segredo = SegredoDispositivoHasher.GerarSegredo();
        var leitor = new DispositivoAgenteBiometrico { ObraId = cuia.Id, Nome = "Leitor Cuiá", SegredoHash = SegredoDispositivoHasher.GerarHash(segredo) };
        db.DispositivosAgenteBiometrico.Add(leitor);
        await db.SaveChangesAsync();
        return new Cenario(db, leitor, segredo, instrutor);
    }

    private static FutronicAutenticacaoStrategy CriarStrategy(SstDbContext db)
    {
        var autenticador = new DispositivoAgenteAutenticador(db, new SegredoDispositivoHasherService());
        return new FutronicAutenticacaoStrategy(db, autenticador, Options.Create(new AssinaturaOptions { LimiarConfiancaBiometriaLocal = 50 }));
    }

    [Fact]
    public async Task InstrutorAutorizado_AutenticaNoLeitorDeOutraObra_QuandoPedidoComoInstrutor()
    {
        var c = await PrepararAsync(nameof(InstrutorAutorizado_AutenticaNoLeitorDeOutraObra_QuandoPedidoComoInstrutor), autorizadoOutrasObras: true);

        var resultado = await CriarStrategy(c.Db).AutenticarPorMatchLocalAsync(
            c.LeitorCuia.Id, c.Segredo, c.Instrutor.Id, 80, null, CancellationToken.None, permitirInstrutorDeOutraObra: true);

        Assert.Equal(c.Instrutor.Id, resultado.TrabalhadorId);
        Assert.Equal(MetodoAutenticacaoAssinatura.Biometria, resultado.Metodo);
    }

    [Fact]
    public async Task InstrutorAutorizado_ContinuaRecusadoEmOutroFluxo()
    {
        var c = await PrepararAsync(nameof(InstrutorAutorizado_ContinuaRecusadoEmOutroFluxo), autorizadoOutrasObras: true);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => CriarStrategy(c.Db).AutenticarPorMatchLocalAsync(
            c.LeitorCuia.Id, c.Segredo, c.Instrutor.Id, 80, null, CancellationToken.None));
    }

    [Fact]
    public async Task InstrutorSemAutorizacaoParaOutrasObras_EhRecusado()
    {
        var c = await PrepararAsync(nameof(InstrutorSemAutorizacaoParaOutrasObras_EhRecusado), autorizadoOutrasObras: false);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => CriarStrategy(c.Db).AutenticarPorMatchLocalAsync(
            c.LeitorCuia.Id, c.Segredo, c.Instrutor.Id, 80, null, CancellationToken.None, permitirInstrutorDeOutraObra: true));
    }

    [Fact]
    public async Task TrabalhadorSemPerfilDeInstrutor_EhRecusadoMesmoPedindoComoInstrutor()
    {
        var c = await PrepararAsync(nameof(TrabalhadorSemPerfilDeInstrutor_EhRecusadoMesmoPedindoComoInstrutor), autorizadoOutrasObras: true, comPerfil: false);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => CriarStrategy(c.Db).AutenticarPorMatchLocalAsync(
            c.LeitorCuia.Id, c.Segredo, c.Instrutor.Id, 80, null, CancellationToken.None, permitirInstrutorDeOutraObra: true));
    }

    [Fact]
    public async Task Sincronizacao_EnviaADigitalDoInstrutorAutorizadoParaOLeitorDaOutraObra()
    {
        var c = await PrepararAsync(nameof(Sincronizacao_EnviaADigitalDoInstrutorAutorizadoParaOLeitorDaOutraObra), autorizadoOutrasObras: true);
        var handler = new SincronizarTemplatesQueryHandler(c.Db, new DispositivoAgenteAutenticador(c.Db, new SegredoDispositivoHasherService()));

        var resultado = await handler.Handle(new SincronizarTemplatesQuery(c.LeitorCuia.Id, c.Segredo), CancellationToken.None);

        Assert.Contains(resultado, t => t.TrabalhadorId == c.Instrutor.Id);
    }

    [Fact]
    public async Task Sincronizacao_NaoEnviaADigitalDoInstrutorSemAutorizacao()
    {
        var c = await PrepararAsync(nameof(Sincronizacao_NaoEnviaADigitalDoInstrutorSemAutorizacao), autorizadoOutrasObras: false);
        var handler = new SincronizarTemplatesQueryHandler(c.Db, new DispositivoAgenteAutenticador(c.Db, new SegredoDispositivoHasherService()));

        var resultado = await handler.Handle(new SincronizarTemplatesQuery(c.LeitorCuia.Id, c.Segredo), CancellationToken.None);

        Assert.DoesNotContain(resultado, t => t.TrabalhadorId == c.Instrutor.Id);
    }
}

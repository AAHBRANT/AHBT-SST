using System.Drawing;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using AAHBRANT.SST.AgenteCameras.Envio;
using AAHBRANT.SST.AgenteCameras.Infra;
using AAHBRANT.SST.AgenteCameras.Opcoes;
using AAHBRANT.SST.AgenteCameras.Quadros;
using AAHBRANT.SST.AgenteCameras.Seguranca;
using AAHBRANT.SST.AgenteCameras.Servidor;
using Microsoft.Extensions.Logging.Abstractions;

namespace AAHBRANT.SST.AgenteCameras.Tests;

// Etapa 5: configuração vinda do servidor, reconciliação das câmeras e testes de conexão.
internal sealed class ClienteConfigFalso : IClienteConfiguracao
{
    private readonly Queue<ResultadoConsulta> _consultas = new();
    public List<(Guid Id, ResultadoTesteConexao Resultado)> Resultados { get; } = new();
    public Queue<TipoEnvioTeste> RespostasEnvio { get; } = new();
    public int Consultas;

    public void Enfileirar(params ResultadoConsulta[] r) { foreach (var x in r) _consultas.Enqueue(x); }

    public static ResultadoConsulta Ok(IEnumerable<CameraServidor>? cameras = null, IEnumerable<TesteServidor>? testes = null, int intervalo = 60) =>
        new(TipoConsulta.Sucesso, 200, new ConfiguracaoServidor(intervalo, cameras?.ToList() ?? new(), testes?.ToList() ?? new()), null, null);

    public Task<ResultadoConsulta> ConsultarAsync(CancellationToken ct)
    {
        Interlocked.Increment(ref Consultas);
        lock (_consultas) return Task.FromResult(_consultas.Count > 0 ? _consultas.Dequeue() : ResultadoConsulta.Indisp("sem rede"));
    }

    public Task<TipoEnvioTeste> EnviarResultadoTesteAsync(Guid testeId, ResultadoTesteConexao resultado, CancellationToken ct)
    {
        lock (Resultados) Resultados.Add((testeId, resultado));
        lock (RespostasEnvio) return Task.FromResult(RespostasEnvio.Count > 0 ? RespostasEnvio.Dequeue() : TipoEnvioTeste.Enviado);
    }
}

public class ReconciliadorCamerasTests
{
    private sealed class Painel
    {
        public readonly object Trava = new();
        public List<string> Iniciadas = new();
        public List<string> Encerradas = new();

        public async Task Executar(CameraConfig c, CancellationToken ct)
        {
            lock (Trava) Iniciadas.Add(c.CameraId + "@" + c.Senha);
            try { await Task.Delay(Timeout.Infinite, ct); }
            finally { lock (Trava) Encerradas.Add(c.CameraId + "@" + c.Senha); }
        }

        public string[] I() { lock (Trava) return Iniciadas.ToArray(); }
        public string[] E() { lock (Trava) return Encerradas.ToArray(); }
    }

    private static ReconciliadorCameras Novo(Painel p, CancellationToken parada = default) =>
        new(p.Executar, parada, NullLogger.Instance, TimeSpan.FromSeconds(5));

    [Fact]
    public async Task IniciaCameraNovaEMantemQuemNaoMudou()
    {
        var p = new Painel();
        var rec = Novo(p);
        var a = Modelos.Camera("a");
        var b = Modelos.Camera("b");

        var r1 = await rec.ReconciliarAsync(new[] { a });
        var r2 = await rec.ReconciliarAsync(new[] { a, b });

        Assert.Equal(new[] { "a" }, r1.Iniciadas);
        Assert.Equal(new[] { "b" }, r2.Iniciadas);
        Assert.Equal(new[] { "a" }, r2.Mantidas);
        Assert.False(r2.Paradas.Any() || r2.Reiniciadas.Any());
        Assert.Equal(2, p.I().Length);           // "a" foi iniciada UMA vez só
        Assert.Empty(p.E());
        await rec.ReconciliarAsync(Array.Empty<CameraConfig>());
    }

    [Fact]
    public async Task ParaCameraRemovidaSemTocarNasOutras()
    {
        var p = new Painel();
        var rec = Novo(p);
        await rec.ReconciliarAsync(new[] { Modelos.Camera("a"), Modelos.Camera("b") });

        var r = await rec.ReconciliarAsync(new[] { Modelos.Camera("b") });

        Assert.Equal(new[] { "a" }, r.Paradas);
        Assert.Equal(new[] { "b" }, r.Mantidas);
        Assert.Equal(new[] { "a@s3nh@Secreta" }, p.E());
        Assert.Equal(new[] { "b" }, rec.EmExecucao.Keys.ToArray());
        await rec.ReconciliarAsync(Array.Empty<CameraConfig>());
    }

    [Theory]
    [InlineData("host")]
    [InlineData("porta")]
    [InlineData("usuario")]
    [InlineData("senha")]
    [InlineData("canal")]
    [InlineData("stream")]
    [InlineData("intervalo")]
    public async Task ReiniciaQuandoUmaCampoDeLeituraMuda(string campo)
    {
        var p = new Painel();
        var rec = Novo(p);
        var antes = Modelos.Camera("a");
        await rec.ReconciliarAsync(new[] { antes, Modelos.Camera("b") });
        var depois = campo switch
        {
            "host" => antes with { Host = "10.0.0.9" },
            "porta" => antes with { PortaRtsp = 8554 },
            "usuario" => antes with { Usuario = "outro" },
            "senha" => antes with { Senha = "nova-senha" },
            "canal" => antes with { Canal = 2 },
            "stream" => antes with { Stream = 0 },
            _ => antes with { IntervaloEnvio = TimeSpan.FromSeconds(9) },
        };

        var r = await rec.ReconciliarAsync(new[] { depois, Modelos.Camera("b") });

        Assert.Equal(new[] { "a" }, r.Reiniciadas);
        Assert.Equal(new[] { "b" }, r.Mantidas);
        Assert.Equal(depois, rec.EmExecucao["a"]);
        Assert.Single(p.E());                     // só a antiga "a" parou; "b" seguiu
        Assert.Equal(3, p.I().Length);
        await rec.ReconciliarAsync(Array.Empty<CameraConfig>());
    }

    [Fact]
    public async Task MudarSoONomeNaoReinicia()
    {
        var p = new Painel();
        var rec = Novo(p);
        await rec.ReconciliarAsync(new[] { Modelos.Camera("a", nome: "Portaria") });

        var r = await rec.ReconciliarAsync(new[] { Modelos.Camera("a", nome: "Portaria Sul") });

        Assert.Equal(new[] { "a" }, r.Mantidas);
        Assert.Single(p.I());
        await rec.ReconciliarAsync(Array.Empty<CameraConfig>());
    }

    [Fact]
    public async Task CameraQueFalhaSozinhaNaoDerrubaAsOutras()
    {
        var iniciadas = 0;
        var rec = new ReconciliadorCameras(async (c, ct) =>
        {
            Interlocked.Increment(ref iniciadas);
            if (c.CameraId == "ruim") throw new InvalidOperationException("quebrou");
            await Task.Delay(Timeout.Infinite, ct);
        }, CancellationToken.None, NullLogger.Instance, TimeSpan.FromSeconds(5));

        await rec.ReconciliarAsync(new[] { Modelos.Camera("ruim"), Modelos.Camera("boa") });
        await Task.Delay(100);

        Assert.Equal(2, rec.EmExecucao.Count);
        var r = await rec.ReconciliarAsync(Array.Empty<CameraConfig>());
        Assert.Equal(2, r.Paradas.Count);
    }

    [Fact]
    public async Task PararOServicoEncerraTodas()
    {
        var p = new Painel();
        using var parada = new CancellationTokenSource();
        var rec = Novo(p, parada.Token);
        await rec.ReconciliarAsync(new[] { Modelos.Camera("a"), Modelos.Camera("b") });

        parada.Cancel();
        await rec.AguardarTodasAsync();

        Assert.Equal(2, p.E().Length);
    }
}

public class SincronizadorConfiguracaoTests : IDisposable
{
    private readonly PastaTemporaria _pasta = new();
    private readonly RelogioFalso _relogio = new();
    private readonly Mascarador _mascarador = new();
    private readonly LogColetor _log = new();
    private readonly ClienteConfigFalso _cliente = new();
    private readonly EstadoApi _estado = new();
    private readonly List<CameraConfig> _rodando = new();
    private readonly object _trava = new();

    public void Dispose() => _pasta.Dispose();

    private static readonly Guid IdA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid IdB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private static CameraServidor Cam(Guid id, string host = "192.168.0.8", string senha = "Senha-Do-Servidor", string nome = "Portaria", int canal = 1) =>
        new(id, nome, Guid.NewGuid(), host, 554, "admin", senha, canal, 1, 3);

    private ReconciliadorCameras NovoReconciliador() => new(async (c, ct) =>
    {
        lock (_trava) _rodando.Add(c);
        await Task.Delay(Timeout.Infinite, ct);
    }, CancellationToken.None, NullLogger.Instance, TimeSpan.FromSeconds(5));

    private SincronizadorConfiguracao Novo(ReconciliadorCameras rec, IReadOnlyList<CameraConfig>? locais = null, CacheCameras? cache = null) =>
        new(_cliente, rec, null, cache, locais ?? Array.Empty<CameraConfig>(), _estado, _relogio, _mascarador, _log);

    private CacheCameras NovoCache() => new(_pasta.Caminho, new ProtetorFalso(), "agente-1", _log);

    [Fact]
    public async Task ReconciliaComAListaDoServidorEUsaOIntervaloDevolvido()
    {
        var rec = NovoReconciliador();
        var s = Novo(rec);
        _cliente.Enfileirar(ClienteConfigFalso.Ok(new[] { Cam(IdA) }, intervalo: 45));

        var espera = await s.PassoAsync(CancellationToken.None);

        Assert.Equal(TimeSpan.FromSeconds(45), espera);
        Assert.Equal(new[] { IdA.ToString("D") }, rec.EmExecucao.Keys.ToArray());
        Assert.Equal("Senha-Do-Servidor", rec.EmExecucao[IdA.ToString("D")].Senha);
        await rec.ReconciliarAsync(Array.Empty<CameraConfig>());
    }

    [Fact]
    public async Task IntervaloInvalidoVoltaParaSessentaSegundos()
    {
        var s = Novo(NovoReconciliador());
        _cliente.Enfileirar(ClienteConfigFalso.Ok(intervalo: 0));
        Assert.Equal(TimeSpan.FromSeconds(60), await s.PassoAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CameraNovaMudadaERemovidaNoServidorSeguemOCadastro()
    {
        var rec = NovoReconciliador();
        var s = Novo(rec);
        _cliente.Enfileirar(
            ClienteConfigFalso.Ok(new[] { Cam(IdA), Cam(IdB) }),
            ClienteConfigFalso.Ok(new[] { Cam(IdA, host: "10.1.1.1") }));

        await s.PassoAsync(CancellationToken.None);
        Assert.Equal(2, rec.EmExecucao.Count);
        await s.PassoAsync(CancellationToken.None);

        Assert.Equal(new[] { IdA.ToString("D") }, rec.EmExecucao.Keys.ToArray());
        Assert.Equal("10.1.1.1", rec.EmExecucao[IdA.ToString("D")].Host);
        lock (_trava) Assert.Equal(3, _rodando.Count); // A, B e A de novo (reiniciada)
        await rec.ReconciliarAsync(Array.Empty<CameraConfig>());
    }

    [Fact]
    public async Task ServidorForaDoArMantemAUltimaConfiguracao()
    {
        var rec = NovoReconciliador();
        var s = Novo(rec);
        _cliente.Enfileirar(
            ClienteConfigFalso.Ok(new[] { Cam(IdA) }),
            ResultadoConsulta.Indisp("erro do servidor (HTTP 503)", 503),
            new ResultadoConsulta(TipoConsulta.LimiteTaxa, 429, null, TimeSpan.FromSeconds(20), null),
            ResultadoConsulta.Indisp("tempo esgotado"));

        await s.PassoAsync(CancellationToken.None);
        var e1 = await s.PassoAsync(CancellationToken.None);
        var e2 = await s.PassoAsync(CancellationToken.None);
        await s.PassoAsync(CancellationToken.None);

        Assert.Equal(TimeSpan.FromSeconds(60), e1);
        Assert.Equal(TimeSpan.FromSeconds(20), e2);
        Assert.Equal(new[] { IdA.ToString("D") }, rec.EmExecucao.Keys.ToArray());
        lock (_trava) Assert.Single(_rodando);    // nunca foi reiniciada
        Assert.Equal(3, s.FalhasSeguidas);
        await rec.ReconciliarAsync(Array.Empty<CameraConfig>());
    }

    [Fact]
    public async Task Erro401PausaOsEnviosMasNaoDesligaCameras()
    {
        var rec = NovoReconciliador();
        var s = Novo(rec);
        _cliente.Enfileirar(
            ClienteConfigFalso.Ok(new[] { Cam(IdA) }),
            new ResultadoConsulta(TipoConsulta.SegredoInvalido, 401, null, null, "segredo"));

        await s.PassoAsync(CancellationToken.None);
        var espera = await s.PassoAsync(CancellationToken.None);

        Assert.Equal(SincronizadorConfiguracao.PausaSegredoInvalido, espera);
        Assert.True(_estado.PausadoAte > _relogio.Agora);
        Assert.Single(rec.EmExecucao);
        Assert.Contains(_log.Linhas, l => l.Nivel == Microsoft.Extensions.Logging.LogLevel.Error && l.Texto.Contains("401"));
        await rec.ReconciliarAsync(Array.Empty<CameraConfig>());
    }

    private static ResultadoConsulta Recusado() => new(TipoConsulta.SegredoInvalido, 401, null, null, "segredo");

    [Fact]
    public async Task AgenteRevogado_DepoisDeCincoRespostas401Seguidas_ParaAsCamerasDoServidorEApagaOCache_MantendoAsLocais()
    {
        var local = Modelos.Camera("local-1");
        var rec = NovoReconciliador();
        var cache = NovoCache();
        var s = Novo(rec, new[] { local }, cache);
        _cliente.Enfileirar(ClienteConfigFalso.Ok(new[] { Cam(IdA) }));
        for (var i = 0; i < SincronizadorConfiguracao.LimiteRespostas401; i++) _cliente.Enfileirar(Recusado());

        await s.PassoAsync(CancellationToken.None);
        Assert.True(File.Exists(cache.Caminho));
        for (var i = 1; i < SincronizadorConfiguracao.LimiteRespostas401; i++)
        {
            await s.PassoAsync(CancellationToken.None);
            Assert.Contains(IdA.ToString("D"), rec.EmExecucao.Keys);     // ainda tolera tropeços: 4 respostas 401 não bastam
            Assert.True(File.Exists(cache.Caminho));
        }

        await s.PassoAsync(CancellationToken.None);                      // a quinta

        Assert.Equal(new[] { local.CameraId }, rec.EmExecucao.Keys.ToArray());   // só a do arquivo local segue
        Assert.False(File.Exists(cache.Caminho));
        Assert.Empty(s.DoServidor);
        Assert.Contains(_log.Linhas, l => l.Nivel == Microsoft.Extensions.Logging.LogLevel.Error && l.Texto.Contains("foram paradas"));
        await rec.ReconciliarAsync(Array.Empty<CameraConfig>());
    }

    [Fact]
    public async Task RespostaBemSucedidaZeraAContagemDe401()
    {
        var rec = NovoReconciliador();
        var s = Novo(rec, cache: NovoCache());
        _cliente.Enfileirar(ClienteConfigFalso.Ok(new[] { Cam(IdA) }));
        for (var i = 0; i < SincronizadorConfiguracao.LimiteRespostas401 - 1; i++) _cliente.Enfileirar(Recusado());
        _cliente.Enfileirar(ClienteConfigFalso.Ok(new[] { Cam(IdA) }));
        for (var i = 0; i < SincronizadorConfiguracao.LimiteRespostas401 - 1; i++) _cliente.Enfileirar(Recusado());

        for (var i = 0; i < 2 * SincronizadorConfiguracao.LimiteRespostas401; i++) await s.PassoAsync(CancellationToken.None);

        Assert.Single(rec.EmExecucao);               // nunca chegou a 5 seguidas
        Assert.Equal(SincronizadorConfiguracao.LimiteRespostas401 - 1, s.Respostas401Seguidas);
        await rec.ReconciliarAsync(Array.Empty<CameraConfig>());
    }

    [Fact]
    public async Task DepoisDeRevogado_VoltandoAAutenticar_RetomaAsCamerasDoServidor()
    {
        var rec = NovoReconciliador();
        var cache = NovoCache();
        var s = Novo(rec, cache: cache);
        _cliente.Enfileirar(ClienteConfigFalso.Ok(new[] { Cam(IdA) }));
        for (var i = 0; i < SincronizadorConfiguracao.LimiteRespostas401; i++) _cliente.Enfileirar(Recusado());
        _cliente.Enfileirar(ClienteConfigFalso.Ok(new[] { Cam(IdA), Cam(IdB) }));

        for (var i = 0; i < SincronizadorConfiguracao.LimiteRespostas401 + 1; i++) await s.PassoAsync(CancellationToken.None);
        Assert.Empty(rec.EmExecucao);
        Assert.False(File.Exists(cache.Caminho));

        await s.PassoAsync(CancellationToken.None);

        Assert.Equal(2, rec.EmExecucao.Count);
        Assert.Equal(0, s.Respostas401Seguidas);
        Assert.True(File.Exists(cache.Caminho));     // o cache é refeito
        await rec.ReconciliarAsync(Array.Empty<CameraConfig>());
    }

    [Fact]
    public async Task Respostas401_SemCamerasDoServidor_ContinuamSoPausando()
    {
        var rec = NovoReconciliador();
        var s = Novo(rec, cache: NovoCache());
        for (var i = 0; i < SincronizadorConfiguracao.LimiteRespostas401 + 2; i++) _cliente.Enfileirar(Recusado());

        for (var i = 0; i < SincronizadorConfiguracao.LimiteRespostas401 + 2; i++)
            Assert.Equal(SincronizadorConfiguracao.PausaSegredoInvalido, await s.PassoAsync(CancellationToken.None));

        Assert.True(_estado.PausadoAte > _relogio.Agora);
    }

    [Fact]
    public async Task ServidorMandaQuandoACameraIdRepete_LocalContinuaValendoParaAsOutras()
    {
        var rec = NovoReconciliador();
        var local = Modelos.Camera(IdA.ToString("D"), senha: "senha-local") with { Host = "1.1.1.1" };
        var outraLocal = Modelos.Camera("local-2");
        var s = Novo(rec, new[] { local, outraLocal });
        await s.AplicarCacheAsync();
        Assert.Equal("1.1.1.1", rec.EmExecucao[IdA.ToString("D")].Host);

        _cliente.Enfileirar(ClienteConfigFalso.Ok(new[] { Cam(IdA, host: "9.9.9.9") }));
        await s.PassoAsync(CancellationToken.None);

        Assert.Equal("9.9.9.9", rec.EmExecucao[IdA.ToString("D")].Host);
        Assert.True(rec.EmExecucao.ContainsKey("local-2"));
        await rec.ReconciliarAsync(Array.Empty<CameraConfig>());
    }

    [Fact]
    public async Task CameraRemovidaDoServidorVoltaParaALocalDeMesmoId()
    {
        var rec = NovoReconciliador();
        var local = Modelos.Camera(IdA.ToString("D")) with { Host = "1.1.1.1" };
        var s = Novo(rec, new[] { local });
        _cliente.Enfileirar(ClienteConfigFalso.Ok(new[] { Cam(IdA, host: "9.9.9.9") }), ClienteConfigFalso.Ok());

        await s.PassoAsync(CancellationToken.None);
        await s.PassoAsync(CancellationToken.None);

        Assert.Equal("1.1.1.1", rec.EmExecucao[IdA.ToString("D")].Host);
        await rec.ReconciliarAsync(Array.Empty<CameraConfig>());
    }

    [Fact]
    public async Task CameraInvalidaDoServidorEIgnoradaComAviso()
    {
        var rec = NovoReconciliador();
        var s = Novo(rec);
        _cliente.Enfileirar(ClienteConfigFalso.Ok(new[] { Cam(IdA, canal: 7), Cam(IdB, senha: "") }));

        await s.PassoAsync(CancellationToken.None);

        Assert.Empty(rec.EmExecucao);
        Assert.Contains(_log.Linhas, l => l.Texto.Contains("ignorada"));
    }

    [Fact]
    public async Task ReinicioSemInternetReligaComOCacheProtegido()
    {
        // 1ª vida do serviço: consulta com sucesso e grava o cache.
        var rec1 = NovoReconciliador();
        var s1 = Novo(rec1, cache: NovoCache());
        _cliente.Enfileirar(ClienteConfigFalso.Ok(new[] { Cam(IdA, senha: "Segredo-Super-Unico") }));
        await s1.PassoAsync(CancellationToken.None);
        await rec1.ReconciliarAsync(Array.Empty<CameraConfig>());

        var arquivo = Path.Combine(_pasta.Caminho, CacheCameras.NomeArquivo);
        Assert.True(File.Exists(arquivo));
        Assert.DoesNotContain("Segredo-Super-Unico", Encoding.UTF8.GetString(File.ReadAllBytes(arquivo)));      // protegido em disco
        Assert.DoesNotContain("Segredo-Super-Unico", Encoding.Latin1.GetString(File.ReadAllBytes(arquivo)));

        // 2ª vida: servidor continua fora do ar; as câmeras sobem do cache.
        var rec2 = NovoReconciliador();
        var s2 = Novo(rec2, cache: NovoCache());
        await s2.AplicarCacheAsync();
        await s2.PassoAsync(CancellationToken.None); // sem resposta (indisponível)

        Assert.Equal("Segredo-Super-Unico", rec2.EmExecucao[IdA.ToString("D")].Senha);
        await rec2.ReconciliarAsync(Array.Empty<CameraConfig>());
    }

    [Fact]
    public void CacheDeOutroAgenteOuCorrompidoEIgnoradoSemExcecao()
    {
        var cache = NovoCache();
        cache.Salvar(new[] { Modelos.Camera("a") });
        var outro = new CacheCameras(_pasta.Caminho, new ProtetorFalso(), "agente-2", _log);
        Assert.Empty(outro.Carregar());

        File.WriteAllBytes(cache.Caminho, new byte[] { 1, 2, 3 });
        Assert.Empty(NovoCache().Carregar());
        Assert.DoesNotContain(_log.Linhas, l => l.Texto.Contains("s3nh@Secreta"));
    }

    [Fact]
    public async Task SenhaDoServidorNuncaApareceNoLog()
    {
        var rec = NovoReconciliador();
        var s = Novo(rec);
        const string senha = "Ultra-Secreta-123";
        _cliente.Enfileirar(
            ClienteConfigFalso.Ok(new[] { Cam(IdA, senha: senha) }),
            ResultadoConsulta.Indisp($"falha em rtsp://admin:{senha}@192.168.0.8:554/x senha {senha}"));

        await s.PassoAsync(CancellationToken.None);
        await s.PassoAsync(CancellationToken.None);

        Assert.DoesNotContain(senha, _log.Tudo());
        Assert.Contains(Mascarador.Marca, _log.Tudo());
        // e o mascarador passou a conhecer a senha do servidor
        Assert.DoesNotContain(senha, _mascarador.Mascarar($"x {senha} y"));
        await rec.ReconciliarAsync(Array.Empty<CameraConfig>());
    }

    [Fact]
    public void ToStringDosModelosDoServidorNaoTrazASenha()
    {
        Assert.DoesNotContain("Senha-Do-Servidor", Cam(IdA).ToString());
        Assert.DoesNotContain("Senha-Do-Servidor", new TesteServidor(Guid.NewGuid(), Guid.NewGuid(), null, "h", 554, "u", "Senha-Do-Servidor", 1, 0).ToString());
    }

    [Fact]
    public async Task ExecutarAsyncConsultaNoIntervaloEParaComOCancelamento()
    {
        var rec = NovoReconciliador();
        var s = Novo(rec);
        for (var i = 0; i < 5; i++) _cliente.Enfileirar(ClienteConfigFalso.Ok(new[] { Cam(IdA) }, intervalo: 30));
        using var cts = new CancellationTokenSource();

        var tarefa = Task.Run(() => s.ExecutarAsync(cts.Token));
        var limite = DateTime.UtcNow.AddSeconds(10);
        while (_cliente.Consultas < 3 && DateTime.UtcNow < limite) await Task.Delay(10);
        cts.Cancel();
        await tarefa.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(_cliente.Consultas >= 3);
        Assert.Contains(TimeSpan.FromSeconds(30), _relogio.Atrasos);
        await rec.ReconciliarAsync(Array.Empty<CameraConfig>());
    }
}

public class ProcessadorTestesConexaoTests
{
    private readonly RelogioFalso _relogio = new();
    private readonly Mascarador _mascarador = new();
    private readonly LogColetor _log = new();
    private readonly FonteFalsa _fonte = new();
    private readonly ClienteConfigFalso _cliente = new();
    private const string Senha = "P@ss-Teste-9";

    private ProcessadorTestesConexao Novo(TimeSpan? captura = null, int simultaneos = 2) =>
        new(_fonte, _cliente, _mascarador, _relogio, _log, CancellationToken.None, captura ?? TimeSpan.FromSeconds(5),
            maximoSimultaneos: simultaneos, pausaReenvio: TimeSpan.FromMilliseconds(1));

    private static TesteServidor Teste(Guid? id = null, string senha = Senha) =>
        new(id ?? Guid.NewGuid(), Guid.NewGuid(), null, "192.168.0.8", 554, "admin", senha, 1, 1);

    private static Func<CancellationToken, IAsyncEnumerable<byte[]>> UmQuadro(byte[] jpeg) => ct => Gerar(jpeg, ct);

    private static async IAsyncEnumerable<byte[]> Gerar(byte[] jpeg, [EnumeratorCancellation] CancellationToken ct)
    {
        await Task.Yield();
        yield return jpeg;
        await Task.Delay(Timeout.Infinite, ct);
    }

    private ResultadoTesteConexao Unico() { lock (_cliente.Resultados) return Assert.Single(_cliente.Resultados).Resultado; }

    [Fact]
    public async Task Sucesso_EnviaMiniaturaJpegAte150KbSemMotivo()
    {
        _fonte.Enfileirar(UmQuadro(Modelos.JpegSintetico(Color.SteelBlue, 1920, 1080)));
        var p = Novo();

        p.Aceitar(new[] { Teste() });
        await p.AguardarTodosAsync();

        var r = Unico();
        Assert.True(r.Sucesso);
        Assert.Null(r.Motivo);
        Assert.Contains("1920x1080", r.Detalhe);
        var bytes = Convert.FromBase64String(r.MiniaturaBase64!);
        Assert.True(bytes.Length <= 150 * 1024);
        Assert.Equal(new byte[] { 0xFF, 0xD8 }, bytes[..2]);
    }

    [Fact]
    public void Miniatura_QuadroGrandeComRuidoFicaAte150Kb()
    {
        var rnd = new Random(7);
        using var bmp = new Bitmap(2592, 1944);
        for (var y = 0; y < bmp.Height; y += 1)
            for (var x = 0; x < bmp.Width; x += 3)
                bmp.SetPixel(x, y, Color.FromArgb(rnd.Next(256), rnd.Next(256), rnd.Next(256)));
        using var ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);

        var m = Miniatura.Gerar(ms.ToArray());

        Assert.NotNull(m);
        Assert.True(m!.Value.Jpeg.Length <= Miniatura.LimiteBytes);
        Assert.Equal(2592, m.Value.Largura);
    }

    [Fact]
    public void Miniatura_LixoNaoEJpeg_DevolveNulo() =>
        Assert.Null(Miniatura.Gerar(new byte[] { 1, 2, 3, 4 }));

    [Fact]
    public async Task ExecutaCadaTesteIdUmaUnicaVez()
    {
        _fonte.Padrao = UmQuadro(Modelos.JpegSintetico(Color.Gray));
        var p = Novo();
        var id = Guid.NewGuid();

        var a = p.Aceitar(new[] { Teste(id) });
        await p.AguardarTodosAsync();
        var b = p.Aceitar(new[] { Teste(id), Teste(id) });   // o servidor repete o pedido até ouvir a resposta
        var c = p.Aceitar(new[] { Teste(id) });
        await p.AguardarTodosAsync();

        Assert.Equal(1, a);
        Assert.Equal(0, b);
        Assert.Equal(0, c);
        Assert.Equal(1, _fonte.Chamadas);
        Assert.Single(_cliente.Resultados);
    }

    [Fact]
    public async Task DoisPedidosDistintosNaMesmaConsultaRodamOsDois()
    {
        _fonte.Padrao = UmQuadro(Modelos.JpegSintetico(Color.Gray));
        var p = Novo();

        Assert.Equal(2, p.Aceitar(new[] { Teste(), Teste() }));
        await p.AguardarTodosAsync();

        Assert.Equal(2, _cliente.Resultados.Count);
    }

    [Fact]
    public async Task TesteNaoEncontrado404EIgnoradoSemReenviar()
    {
        _fonte.Padrao = UmQuadro(Modelos.JpegSintetico(Color.Gray));
        _cliente.RespostasEnvio.Enqueue(TipoEnvioTeste.TesteNaoEncontrado);
        var p = Novo();

        p.Aceitar(new[] { Teste() });
        await p.AguardarTodosAsync();

        Assert.Single(_cliente.Resultados);
        Assert.DoesNotContain(_log.Linhas, l => l.Nivel >= Microsoft.Extensions.Logging.LogLevel.Warning);
    }

    [Fact]
    public async Task FalhaNoEnvioTentaDeNovoAteTresVezes()
    {
        _fonte.Padrao = UmQuadro(Modelos.JpegSintetico(Color.Gray));
        _cliente.RespostasEnvio.Enqueue(TipoEnvioTeste.Falha);
        _cliente.RespostasEnvio.Enqueue(TipoEnvioTeste.Falha);
        _cliente.RespostasEnvio.Enqueue(TipoEnvioTeste.Falha);
        var p = Novo();

        p.Aceitar(new[] { Teste() });
        await p.AguardarTodosAsync();

        Assert.Equal(3, _cliente.Resultados.Count);
        Assert.Equal(1, _fonte.Chamadas);        // a câmera foi aberta uma vez; só o envio repetiu
    }

    [Fact]
    public async Task CameraSemRespostaViraMotivoDeTempoSemSenha()
    {
        _fonte.Padrao = FonteFalsa.Pendurada;
        var p = Novo(captura: TimeSpan.FromMilliseconds(150));

        p.Aceitar(new[] { Teste() });
        await p.AguardarTodosAsync();

        var r = Unico();
        Assert.False(r.Sucesso);
        Assert.Equal(ClassificadorFalhaConexao.SemResposta, r.Motivo);
        Assert.Equal("sem_resposta", r.Codigo);   // o texto de detalhe ("Sem imagem em ...") não pode mudar a causa
        Assert.Null(r.MiniaturaBase64);
    }

    [Fact]
    public async Task FalhaDoFfmpegNaoVazaSenhaNemUrlNoMotivoDetalheELog()
    {
        var url = $"rtsp://admin:{Senha}@192.168.0.8:554/user=admin&password={Senha}&channel=1&stream=1.sdp?real_stream";
        _fonte.Padrao = FonteFalsa.Falha($"O ffmpeg encerrou (código 1). Saída: [rtsp @ 0x1] method DESCRIBE failed: 401 Unauthorized | {url}: Server returned 401 Unauthorized (authorization failed) senha={Senha}");
        var p = Novo();

        p.Aceitar(new[] { Teste() });
        await p.AguardarTodosAsync();

        var r = Unico();
        Assert.False(r.Sucesso);
        Assert.Equal(ClassificadorFalhaConexao.SenhaRecusada, r.Motivo);
        Assert.Equal("senha_recusada", r.Codigo);
        foreach (var texto in new[] { r.Motivo, r.Detalhe, _log.Tudo() })
        {
            Assert.DoesNotContain(Senha, texto ?? "");
            Assert.DoesNotContain("rtsp://", texto ?? "");
            Assert.DoesNotContain("password=", texto ?? "", StringComparison.OrdinalIgnoreCase);
        }
        Assert.True(r.Detalhe!.Length <= ProcessadorTestesConexao.MaximoDetalhe);
    }

    [Fact]
    public async Task SenhaCurtaTambemNaoVaza()
    {
        _fonte.Padrao = FonteFalsa.Falha("Saída: erro na conta de zz9 com a senha zz9");
        var p = Novo();

        p.Aceitar(new[] { Teste(senha: "zz9") });
        await p.AguardarTodosAsync();

        Assert.DoesNotContain("zz9", Unico().Detalhe);
    }

    [Fact]
    public async Task FfmpegAusenteViraMotivoProprio()
    {
        _fonte.Padrao = _ => throw new FfmpegIndisponivelException("ffmpeg não encontrado em \"C:\\x\\ffmpeg.exe\"");
        var p = Novo();

        p.Aceitar(new[] { Teste() });
        await p.AguardarTodosAsync();

        Assert.Equal(ClassificadorFalhaConexao.FfmpegAusente, Unico().Motivo);
        Assert.Equal("ffmpeg_ausente", Unico().Codigo);
    }

    [Fact]
    public async Task QuadroIlegivelNaoEnviaMiniaturaComoSucesso()
    {
        _fonte.Padrao = UmQuadro(new byte[] { 0xFF, 0xD8, 1, 2, 0xFF, 0xD9 });
        var p = Novo();

        p.Aceitar(new[] { Teste() });
        await p.AguardarTodosAsync();

        var r = Unico();
        Assert.False(r.Sucesso);
        Assert.Equal(ClassificadorFalhaConexao.ImagemInvalida, r.Motivo);
        Assert.Equal("imagem_ilegivel", r.Codigo);
    }

    [Fact]
    public async Task ConexaoEncerradaSemImagemEFalhaSemImagemComCodigoProprio()
    {
        _fonte.Padrao = _ => SemQuadros();
        var p = Novo();

        p.Aceitar(new[] { Teste() });
        await p.AguardarTodosAsync();

        var r = Unico();
        Assert.False(r.Sucesso);
        Assert.Equal("sem_imagem", r.Codigo);
        Assert.Equal(ClassificadorFalhaConexao.SemImagem, r.Motivo);
    }

    [Fact]
    public async Task FalhaDoFfmpegQueEncerraAConexaoTemCodigoDeConexaoEncerrada_NaoDeSenha()
    {
        _fonte.Padrao = FonteFalsa.Falha("O ffmpeg encerrou (código 1). Saída: Connection reset by peer");
        var p = Novo();

        p.Aceitar(new[] { Teste() });
        await p.AguardarTodosAsync();

        Assert.Equal("conexao_encerrada", Unico().Codigo);
    }

    [Fact]
    public async Task SucessoNaoTemCodigo()
    {
        _fonte.Padrao = UmQuadro(Modelos.JpegSintetico(Color.Gray));
        var p = Novo();

        p.Aceitar(new[] { Teste() });
        await p.AguardarTodosAsync();

        Assert.True(Unico().Sucesso);
        Assert.Null(Unico().Codigo);
    }

    private static async IAsyncEnumerable<byte[]> SemQuadros()
    {
        await Task.CompletedTask;
        yield break;
    }

    [Fact]
    public async Task LimitaTestesSimultaneos()
    {
        var atuais = 0;
        var maximo = 0;
        _fonte.Padrao = ct => Lento(ct);
        async IAsyncEnumerable<byte[]> Lento([EnumeratorCancellation] CancellationToken ct)
        {
            var n = Interlocked.Increment(ref atuais);
            maximo = Math.Max(maximo, n);
            await Task.Delay(80, ct);
            Interlocked.Decrement(ref atuais);
            yield return Modelos.JpegSintetico(Color.Gray);
            await Task.Delay(Timeout.Infinite, ct);
        }
        var p = Novo(simultaneos: 2);

        p.Aceitar(Enumerable.Range(0, 5).Select(_ => Teste()));
        await p.AguardarTodosAsync();

        Assert.Equal(5, _cliente.Resultados.Count);
        Assert.True(maximo <= 2, $"máximo simultâneo foi {maximo}");
    }

    [Fact]
    public void CorpoDoResultadoUsaOsNomesDoContrato()
    {
        var json = ClienteConfiguracao.Serializar(new ResultadoTesteConexao(false, "Senha recusada pela câmera", "401", null, "senha_recusada"));
        Assert.Equal("{\"sucesso\":false,\"motivo\":\"Senha recusada pela câmera\",\"detalhe\":\"401\",\"codigo\":\"senha_recusada\"}", json);
    }
}

public class ClassificadorFalhaConexaoTests
{
    [Theory]
    [InlineData("method DESCRIBE failed: 401 Unauthorized", ClassificadorFalhaConexao.SenhaRecusada)]
    [InlineData("Server returned 401 Unauthorized (authorization failed)", ClassificadorFalhaConexao.SenhaRecusada)]
    [InlineData("method DESCRIBE failed: 403 Forbidden", ClassificadorFalhaConexao.AcessoNegado)]
    [InlineData("method DESCRIBE failed: 404 Not Found", ClassificadorFalhaConexao.VideoNaoEncontrado)]
    [InlineData("Connection to tcp://192.168.0.8:554?timeout=0 failed: No route to host", ClassificadorFalhaConexao.NaoEncontradaNaRede)]
    [InlineData("Connection to tcp://x:554 failed: Error number -10065 occurred", ClassificadorFalhaConexao.NaoEncontradaNaRede)]
    [InlineData("Failed to resolve hostname camera-xyz: Name or service not known", ClassificadorFalhaConexao.NaoEncontradaNaRede)]
    [InlineData("Connection to tcp://192.168.0.8:554?timeout=0 failed: Connection refused", ClassificadorFalhaConexao.ConexaoRecusada)]
    [InlineData("failed: Error number -10061 occurred", ClassificadorFalhaConexao.ConexaoRecusada)]
    [InlineData("Connection to tcp://192.168.0.8:554?timeout=0 failed: Connection timed out", ClassificadorFalhaConexao.SemResposta)]
    [InlineData("Error number -10060 occurred", ClassificadorFalhaConexao.SemResposta)]
    [InlineData("Connection reset by peer", ClassificadorFalhaConexao.ConexaoEncerrada)]
    [InlineData("Invalid data found when processing input", ClassificadorFalhaConexao.Generico)]
    [InlineData("", ClassificadorFalhaConexao.Generico)]
    [InlineData(null, ClassificadorFalhaConexao.Generico)]
    public void ClassificaOMotivoEmPortuguesSimples(string? saida, string esperado) =>
        Assert.Equal(esperado, ClassificadorFalhaConexao.Classificar(saida));

    [Theory]
    [InlineData("method DESCRIBE failed: 401 Unauthorized", "senha_recusada")]
    [InlineData("method DESCRIBE failed: 403 Forbidden", "acesso_negado")]
    [InlineData("method DESCRIBE failed: 404 Not Found", "canal_ou_stream_invalido")]
    [InlineData("failed: No route to host", "camera_nao_encontrada")]
    [InlineData("Connection refused", "conexao_recusada")]
    [InlineData("Connection reset by peer", "conexao_encerrada")]
    [InlineData("Connection timed out", "sem_resposta")]
    [InlineData("Invalid data found when processing input", "erro_desconhecido")]
    [InlineData(null, "erro_desconhecido")]
    public void CadaCausaTemCodigoEstavel(string? saida, string codigo) =>
        Assert.Equal(codigo, ClassificadorFalhaConexao.ClassificarComCodigo(saida).Codigo);

    [Fact]
    public void TimeoutNaUrlNaoViraMotivoDeTempo() =>
        Assert.Equal(ClassificadorFalhaConexao.Generico, ClassificadorFalhaConexao.Classificar("Connection to tcp://x:554?timeout=0 failed"));

    [Fact]
    public void SanitizarTiraUrlSenhaLiteralEscapadaECorta()
    {
        var m = new Mascarador();
        var texto = "falha em rtsp://admin:a&b@10.0.0.1/x e senha a&b / a%26b " + new string('x', 200);

        var s = ClassificadorFalhaConexao.Sanitizar(texto, "a&b", m, 100);

        Assert.DoesNotContain("rtsp://", s);
        Assert.DoesNotContain("a&b", s);
        Assert.DoesNotContain("a%26b", s);
        Assert.True(s.Length <= 100);
    }

    [Fact]
    public void UltimaLinhaDaSaidaPegaOFimDaSaidaDoFfmpeg() =>
        Assert.Equal("c", ProcessadorTestesConexao.UltimaLinhaDaSaida("O ffmpeg encerrou (código 1). Saída: a | b | c"));
}

public class ClienteConfiguracaoTests
{
    private sealed class Handler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Resposta { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK);
        public HttpRequestMessage? Ultima { get; private set; }
        public string? Corpo { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Ultima = request;
            Corpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return Resposta(request);
        }
    }

    private static (ClienteConfiguracao, Handler) Criar()
    {
        var h = new Handler();
        return (new ClienteConfiguracao(new HttpClient(h) { Timeout = Timeout.InfiniteTimeSpan }, new Uri("https://api.exemplo.com.br/sst"),
            "3f2504e0-4f89-11d3-9a0c-0305e82c3301", "SEGREDO-123", new Mascarador()), h);
    }

    private static HttpResponseMessage Json(HttpStatusCode s, string json) =>
        new(s) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task ConsultaUsaRotaECabecalhosEInterpretaOContrato()
    {
        var (cliente, h) = Criar();
        var cam = Guid.NewGuid();
        h.Resposta = _ => Json(HttpStatusCode.OK, $$"""
            {"geradaEmUtc":"2026-11-12T10:00:00Z","intervaloConsultaSegundos":60,
             "cameras":[{"cameraId":"{{cam}}","nome":"Portaria","equipamentoId":"{{Guid.NewGuid()}}","host":"192.168.0.8","portaRtsp":554,"portaOnvif":8899,"enderecoMac":null,"usuario":"admin","senha":"abc","canal":1,"stream":1,"intervaloEnvioSegundos":3}],
             "testesConexao":[{"testeId":"{{Guid.NewGuid()}}","equipamentoId":"{{Guid.NewGuid()}}","cameraId":null,"host":"h","portaRtsp":554,"portaOnvif":8899,"enderecoMac":null,"usuario":"u","senha":"s","canal":2,"stream":0}]}
            """);

        var r = await cliente.ConsultarAsync(CancellationToken.None);

        Assert.Equal(TipoConsulta.Sucesso, r.Tipo);
        Assert.Equal("https://api.exemplo.com.br/sst/api/agentes-camera/3f2504e0-4f89-11d3-9a0c-0305e82c3301/configuracao", h.Ultima!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Get, h.Ultima.Method);
        Assert.Equal("SEGREDO-123", h.Ultima.Headers.GetValues("X-Agente-Segredo").Single());
        Assert.Equal(ClienteQuadros.VersaoDoAgente, h.Ultima.Headers.GetValues("X-Agente-Versao").Single());
        var c = Assert.Single(r.Configuracao!.Cameras!);
        Assert.Equal(cam, c.CameraId);
        Assert.Equal("abc", c.Senha);
        var t = Assert.Single(r.Configuracao.TestesConexao!);
        Assert.Equal(2, t.Canal);
        Assert.Null(t.CameraId);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, TipoConsulta.SegredoInvalido)]
    [InlineData(HttpStatusCode.TooManyRequests, TipoConsulta.LimiteTaxa)]
    [InlineData(HttpStatusCode.InternalServerError, TipoConsulta.Indisponivel)]
    [InlineData(HttpStatusCode.NotFound, TipoConsulta.Indisponivel)]
    [InlineData(HttpStatusCode.BadGateway, TipoConsulta.Indisponivel)]
    public async Task StatusDaConsultaViraTipo(HttpStatusCode status, TipoConsulta esperado)
    {
        var (cliente, h) = Criar();
        h.Resposta = _ => new HttpResponseMessage(status);
        Assert.Equal(esperado, (await cliente.ConsultarAsync(CancellationToken.None)).Tipo);
    }

    [Fact]
    public async Task JsonRuimEIndisponibilidade()
    {
        var (cliente, h) = Criar();
        h.Resposta = _ => Json(HttpStatusCode.OK, "<html>não é json</html>");
        Assert.Equal(TipoConsulta.Indisponivel, (await cliente.ConsultarAsync(CancellationToken.None)).Tipo);
    }

    [Fact]
    public async Task ErroDeRedeNaConsultaNaoLancaEMascaraSegredo()
    {
        var h = new Handler { Resposta = _ => throw new HttpRequestException("falha com SEGREDO-123 no caminho") };
        var m = new Mascarador();
        m.Registrar("SEGREDO-123");
        var cliente = new ClienteConfiguracao(new HttpClient(h), new Uri("https://x.com"), "a", "SEGREDO-123", m);

        var r = await cliente.ConsultarAsync(CancellationToken.None);

        Assert.Equal(TipoConsulta.Indisponivel, r.Tipo);
        Assert.DoesNotContain("SEGREDO-123", r.Detalhe);
    }

    [Fact]
    public async Task ResultadoDoTesteVaiParaARotaCertaComJson()
    {
        var (cliente, h) = Criar();
        h.Resposta = _ => new HttpResponseMessage(HttpStatusCode.NoContent);
        var id = Guid.NewGuid();

        var tipo = await cliente.EnviarResultadoTesteAsync(id, new ResultadoTesteConexao(true, null, "ok", "QUJD"), CancellationToken.None);

        Assert.Equal(TipoEnvioTeste.Enviado, tipo);
        Assert.Equal(HttpMethod.Post, h.Ultima!.Method);
        Assert.EndsWith($"/api/agentes-camera/3f2504e0-4f89-11d3-9a0c-0305e82c3301/testes-conexao/{id}/resultado", h.Ultima.RequestUri!.AbsolutePath);
        Assert.Equal("SEGREDO-123", h.Ultima.Headers.GetValues("X-Agente-Segredo").Single());
        Assert.Equal("application/json", h.Ultima.Content!.Headers.ContentType!.MediaType);
        Assert.Equal("{\"sucesso\":true,\"detalhe\":\"ok\",\"miniaturaBase64\":\"QUJD\"}", h.Corpo);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "", TipoEnvioTeste.SegredoInvalido)]
    [InlineData(HttpStatusCode.NotFound, "{\"codigo\":\"teste_nao_encontrado\"}", TipoEnvioTeste.TesteNaoEncontrado)]
    [InlineData(HttpStatusCode.NotFound, "", TipoEnvioTeste.Falha)]
    [InlineData(HttpStatusCode.BadRequest, "{\"erro\":\"x\"}", TipoEnvioTeste.Descartado)]
    [InlineData(HttpStatusCode.TooManyRequests, "", TipoEnvioTeste.Falha)]
    [InlineData(HttpStatusCode.ServiceUnavailable, "", TipoEnvioTeste.Falha)]
    public async Task StatusDoResultadoViraTipo(HttpStatusCode status, string corpo, TipoEnvioTeste esperado)
    {
        var (cliente, h) = Criar();
        h.Resposta = _ => Json(status, corpo);
        var tipo = await cliente.EnviarResultadoTesteAsync(Guid.NewGuid(), new ResultadoTesteConexao(false, "m", null, null), CancellationToken.None);
        Assert.Equal(esperado, tipo);
    }
}

public class ConversorServidorTests
{
    [Fact]
    public void ConverteERegistraASenhaNoMascarador()
    {
        var m = new Mascarador();
        var id = Guid.NewGuid();
        var c = ConversorServidor.Converter(new CameraServidor(id, " Portaria ", Guid.NewGuid(), " 192.168.0.8 ", 554, "admin", "Mascara-Me-1", 2, 0, 5000), m, out var problema);

        Assert.Null(problema);
        Assert.Equal(id.ToString("D"), c!.CameraId);
        Assert.Equal("Portaria", c.Nome);
        Assert.Equal("192.168.0.8", c.Host);
        Assert.Equal(TimeSpan.FromSeconds(600), c.IntervaloEnvio);   // limitado ao máximo do servidor
        Assert.DoesNotContain("Mascara-Me-1", m.Mascarar("x Mascara-Me-1 y"));
    }

    [Theory]
    [InlineData("", 554, 1, 1, "s")]
    [InlineData("h", 0, 1, 1, "s")]
    [InlineData("h", 554, 3, 1, "s")]
    [InlineData("h", 554, 1, 2, "s")]
    [InlineData("h", 554, 1, 1, "")]
    public void RecusaEntradaInvalida(string host, int porta, int canal, int stream, string senha)
    {
        var c = ConversorServidor.Converter(new CameraServidor(Guid.NewGuid(), "n", Guid.NewGuid(), host, porta, "u", senha, canal, stream, 3), new Mascarador(), out var problema);
        Assert.Null(c);
        Assert.NotNull(problema);
    }

    [Fact]
    public void ValidadorLocalEmModoServidorNaoAvisaQueFicaEmEspera()
    {
        var o = new AgenteCamerasOptions
        {
            ApiBaseUrl = "https://api.exemplo.com.br", AgenteId = Guid.NewGuid().ToString(), Segredo = "x", CaminhoFfmpeg = "ffmpeg.exe",
        };
        Assert.DoesNotContain(ValidadorConfiguracao.Validar(o, new ProtetorFalso()).Avisos, a => a.Contains("Nenhuma câmera ativa"));
        o.LerConfiguracaoDoServidor = false;
        Assert.Contains(ValidadorConfiguracao.Validar(o, new ProtetorFalso()).Avisos, a => a.Contains("Nenhuma câmera ativa"));
    }
}

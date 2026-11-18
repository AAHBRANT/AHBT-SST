using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using AAHBRANT.SST.AgenteCameras.AoVivo;
using AAHBRANT.SST.AgenteCameras.Envio;
using AAHBRANT.SST.AgenteCameras.Fila;
using AAHBRANT.SST.AgenteCameras.Infra;
using AAHBRANT.SST.AgenteCameras.Opcoes;
using AAHBRANT.SST.AgenteCameras.Quadros;
using AAHBRANT.SST.AgenteCameras.Seguranca;
using AAHBRANT.SST.AgenteCameras.Servidor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace AAHBRANT.SST.AgenteCameras.Tests;

// Câmera IP "quase ao vivo": parte do agente.

// Relógio monotônico controlável. AvancaNoAtraso=true: cada espera avança o tempo na hora (prazos vencem sem esperar).
// false: o tempo só anda quando o teste manda (Avancar) e a espera dura 5 ms de verdade.
internal sealed class RelogioMonotonicoFalso : IRelogioMonotonico
{
    private long _ticks;
    public bool AvancaNoAtraso { get; set; } = true;
    public TimeSpan Decorrido => TimeSpan.FromTicks(Interlocked.Read(ref _ticks));
    public void Avancar(TimeSpan t) => Interlocked.Add(ref _ticks, t.Ticks);

    public async Task AtrasarAsync(TimeSpan tempo, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (AvancaNoAtraso)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            Avancar(tempo);
        }
        else
        {
            await Task.Delay(5, ct);
        }
    }
}

internal sealed class ClienteAoVivoFalso : IClienteAoVivo
{
    private readonly Queue<ResultadoQuadroAoVivo> _respostas = new();
    private int _simultaneos;

    public List<(Guid SessaoId, byte[] Jpeg, DateTimeOffset Em)> Quadros { get; } = new();
    public List<(Guid SessaoId, string Codigo, string Motivo)> Falhas { get; } = new();
    public ResultadoQuadroAoVivo PadraoQuandoVazio { get; set; } = Continuar();
    public int MaximoSimultaneo { get; private set; }
    public Func<Task>? Bloqueio { get; set; }

    public static ResultadoQuadroAoVivo Continuar() => new(TipoResultadoAoVivo.Continuar, 204, null, null);
    public static ResultadoQuadroAoVivo Encerrada() => new(TipoResultadoAoVivo.Encerrada, 410, null, "encerrada");
    public static ResultadoQuadroAoVivo SemContato() => ResultadoQuadroAoVivo.SemContato("sem rede");

    public void Enfileirar(params ResultadoQuadroAoVivo[] r)
    {
        foreach (var x in r) lock (_respostas) _respostas.Enqueue(x);
    }

    public int QuadrosEnviados { get { lock (Quadros) return Quadros.Count; } }
    public int QuantasFalhas { get { lock (Falhas) return Falhas.Count; } }

    public async Task<ResultadoQuadroAoVivo> EnviarQuadroAsync(Guid sessaoId, byte[] jpeg, DateTimeOffset capturadoEm, CancellationToken ct)
    {
        var agora = Interlocked.Increment(ref _simultaneos);
        lock (Quadros)
        {
            MaximoSimultaneo = Math.Max(MaximoSimultaneo, agora);
            Quadros.Add((sessaoId, jpeg, capturadoEm));
        }
        try
        {
            if (Bloqueio is { } b) await b().ConfigureAwait(false);
            lock (_respostas) return _respostas.Count > 0 ? _respostas.Dequeue() : PadraoQuandoVazio;
        }
        finally
        {
            Interlocked.Decrement(ref _simultaneos);
        }
    }

    public Task<bool> EnviarFalhaAsync(Guid sessaoId, string codigo, string motivo, CancellationToken ct)
    {
        lock (Falhas) Falhas.Add((sessaoId, codigo, motivo));
        return Task.FromResult(true);
    }
}

// Fonte do ao vivo. "Encerradas" conta quantas leituras foram encerradas (o iterador foi descartado): é o que, na fonte
// real, mata o ffmpeg filho. Por padrão entrega um quadro e fica pendurada até o cancelamento.
internal sealed class FonteAoVivoFalsa : IFonteAoVivo
{
    private int _encerradas;
    public List<(CameraConfig Camera, PerfilCaptura Perfil)> Aberturas { get; } = new();
    public Func<CancellationToken, IAsyncEnumerable<byte[]>> Comportamento { get; set; } = UmQuadroEPendura;
    public int Encerradas => Volatile.Read(ref _encerradas);
    public int QuantasAberturas { get { lock (Aberturas) return Aberturas.Count; } }

    public IAsyncEnumerable<byte[]> LerQuadrosAsync(CameraConfig camera, PerfilCaptura perfil, CancellationToken ct)
    {
        lock (Aberturas) Aberturas.Add((camera, perfil));
        return Embrulhar(ct);
    }

    private async IAsyncEnumerable<byte[]> Embrulhar([EnumeratorCancellation] CancellationToken ct)
    {
        try
        {
            await foreach (var q in Comportamento(ct).WithCancellation(ct).ConfigureAwait(false)) yield return q;
        }
        finally
        {
            Interlocked.Increment(ref _encerradas);
        }
    }

    public static readonly byte[] Jpeg = { 0xFF, 0xD8, 1, 2, 3, 0xFF, 0xD9 };

    public static async IAsyncEnumerable<byte[]> UmQuadroEPendura([EnumeratorCancellation] CancellationToken ct)
    {
        yield return Jpeg;
        await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
    }

    public static Func<CancellationToken, IAsyncEnumerable<byte[]>> Falha(string mensagem) => ct => FalhaImpl(mensagem);

    private static async IAsyncEnumerable<byte[]> FalhaImpl(string mensagem)
    {
        await Task.Yield();
        throw new FonteEncerradaException(mensagem);
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }
}

public class AoVivoTests : IDisposable
{
    private static readonly Guid IdCam = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private const string Senha = "s3nh@Secreta";

    private readonly ITestOutputHelper _saida;
    private readonly PastaTemporaria _pasta = new();
    private readonly RelogioFalso _relogio = new();
    private readonly RelogioMonotonicoFalso _mono = new();
    private readonly ClienteAoVivoFalso _cliente = new();
    private readonly FonteAoVivoFalsa _fonte = new();
    private readonly Mascarador _mascarador = new();
    private readonly LogColetor _log = new();

    public AoVivoTests(ITestOutputHelper saida) => _saida = saida;

    public void Dispose() => _pasta.Dispose();

    private static CameraConfig Cam(int stream = 1) => Modelos.Camera(IdCam.ToString("D"), "Portaria", senha: Senha) with { Stream = stream };

    private static SessaoAoVivoServidor Sessao(int segundos = 600, int fps = 2, int largura = 640, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), IdCam, fps, largura, segundos);

    private TransmissaoAoVivo NovaTransmissao(SessaoAoVivoServidor sessao, CameraConfig? camera = null, TimeSpan? semResposta = null) =>
        new(sessao, camera ?? Cam(), _fonte, _cliente, _relogio, _mono, _mascarador, _log, semResposta);

    private GerenciadorAoVivo NovoGerenciador() =>
        new((s, c) => NovaTransmissao(s, c), CancellationToken.None, NullLogger.Instance, TimeSpan.FromSeconds(5));

    private static IReadOnlyDictionary<string, CameraConfig> Cameras(params CameraConfig[] cameras) =>
        cameras.ToDictionary(c => c.CameraId, StringComparer.OrdinalIgnoreCase);

    private static async Task Esperar(Func<bool> condicao, int segundos = 10)
    {
        var limite = DateTime.UtcNow.AddSeconds(segundos);
        while (!condicao() && DateTime.UtcNow < limite) await Task.Delay(10);
        Assert.True(condicao(), "condição não atingida a tempo");
    }

    // ---------------------------------------------------------------- argumentos do ffmpeg

    [Fact]
    public void ArgumentosDaAnaliseSaoIguaisAosDeHoje()
    {
        var esperado = new[]
        {
            "-hide_banner", "-nostdin", "-loglevel", "warning",
            "-rtsp_transport", "tcp",
            "-timeout", "10000000",
            "-i", "rtsp://x",
            "-an",
            "-vf", "fps=1,scale='min(1280,iw)':-2",
            "-f", "image2pipe", "-c:v", "mjpeg", "-q:v", "4",
            "pipe:1",
        };
        Assert.Equal(esperado, FfmpegFonteQuadros.MontarArgumentos("rtsp://x", 10));
        Assert.Equal(esperado, FfmpegFonteQuadros.MontarArgumentos("rtsp://x", 10, PerfilCaptura.Analise));
        Assert.Equal(new PerfilCaptura(1, 1280, 4), PerfilCaptura.Analise);
    }

    [Fact]
    public void ArgumentosDoAoVivoUsamStream1FpsLarguraEQualidade7()
    {
        // A câmera da análise está no stream 0; o ao vivo vai SEMPRE para o stream 1 do mesmo canal.
        var t = NovaTransmissao(Sessao(fps: 2, largura: 640), Cam(stream: 0));
        Assert.Equal(1, t.Camera.Stream);
        Assert.Equal(1, t.Camera.Canal);
        Assert.Equal("192.168.0.8", t.Camera.Host);
        Assert.Equal(Senha, t.Camera.Senha);

        var url = UrlRtsp.Montar(t.Camera);
        var args = FfmpegFonteQuadros.MontarArgumentos(url, 10, t.Perfil);

        Assert.Contains("channel=1&stream=1.sdp", url);
        Assert.Contains("fps=2,scale='min(640,iw)':-2", args);
        Assert.Equal("7", args[args.ToList().IndexOf("-q:v") + 1]);
        Assert.Equal("10000000", args[args.ToList().IndexOf("-timeout") + 1]);
        Assert.DoesNotContain("-rw_timeout", args);
        Assert.Equal("pipe:1", args[^1]);

        var mascarada = string.Join(" ", args.Select(a => _mascarador.Mascarar(a).Replace(Uri.EscapeDataString(Senha), "***")));
        _saida.WriteLine("ARGS_AO_VIVO: ffmpeg " + mascarada.Replace(Uri.EscapeDataString(Senha), "***"));
    }

    [Theory]
    [InlineData(0, 0, 1, 640)]
    [InlineData(9, 5000, 5, 1280)]
    [InlineData(3, 480, 3, 480)]
    public void FpsELarguraForaDosLimitesSaoAjustados(int fps, int largura, int fpsEsperado, int larguraEsperada)
    {
        var t = NovaTransmissao(Sessao(fps: fps, largura: largura));
        Assert.Equal(new PerfilCaptura(fpsEsperado, larguraEsperada, 7), t.Perfil);
    }

    // ---------------------------------------------------------------- parada

    [Fact]
    public async Task ParaSozinhoSemServidorAoFimDoPrazo()
    {
        _cliente.PadraoQuandoVazio = ClienteAoVivoFalso.SemContato(); // servidor fora do ar o tempo todo
        var t = NovaTransmissao(Sessao(segundos: 5));

        await Task.Run(() => t.ExecutarAsync(CancellationToken.None)).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(MotivoParadaAoVivo.PrazoEsgotado, t.Motivo);
        Assert.True(t.Terminou);
        Assert.Equal(1, _fonte.Encerradas);       // a leitura (e, na fonte real, o ffmpeg) foi encerrada
        Assert.Equal(0, _cliente.QuantasFalhas);  // acabar o prazo não é falha
    }

    [Fact]
    public async Task PrazoUsaRelogioMonotonicoENaoADataDoPc()
    {
        // O relógio do PC (data/hora) salta um dia para a frente: nada acontece. O monotônico é que vale.
        _mono.AvancaNoAtraso = false;
        var t = NovaTransmissao(Sessao(segundos: 30));
        var tarefa = Task.Run(() => t.ExecutarAsync(CancellationToken.None));
        await Esperar(() => _cliente.QuadrosEnviados >= 1);

        _relogio.Avancar(TimeSpan.FromDays(1));
        await Task.Delay(100);
        Assert.False(t.Terminou);

        _mono.Avancar(TimeSpan.FromSeconds(31));
        await tarefa.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(MotivoParadaAoVivo.PrazoEsgotado, t.Motivo);
    }

    [Fact]
    public async Task ParaQuandoORecebe410()
    {
        _mono.AvancaNoAtraso = false; // o prazo (600 s) não vence durante o teste
        _cliente.Enfileirar(ClienteAoVivoFalso.Encerrada());
        var t = NovaTransmissao(Sessao());

        await Task.Run(() => t.ExecutarAsync(CancellationToken.None)).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(MotivoParadaAoVivo.SessaoEncerrada, t.Motivo);
        Assert.Equal(1, _fonte.Encerradas);
        Assert.Equal(1, _cliente.QuadrosEnviados);
        Assert.Equal(0, _cliente.QuantasFalhas);
    }

    [Fact]
    public async Task ParaQuandoOServidorFicaSemResponderPeloLimite()
    {
        _cliente.PadraoQuandoVazio = ClienteAoVivoFalso.SemContato();
        var t = NovaTransmissao(Sessao(segundos: 600), semResposta: TimeSpan.FromSeconds(3));

        await Task.Run(() => t.ExecutarAsync(CancellationToken.None)).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(MotivoParadaAoVivo.ServidorSemResposta, t.Motivo);
        Assert.Equal(1, _fonte.Encerradas);
    }

    [Fact]
    public async Task ParaQuandoOServicoPara()
    {
        _mono.AvancaNoAtraso = false;
        var t = NovaTransmissao(Sessao());
        using var cts = new CancellationTokenSource();
        var tarefa = Task.Run(() => t.ExecutarAsync(cts.Token));
        await Esperar(() => _fonte.QuantasAberturas == 1);

        cts.Cancel();
        await tarefa.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(MotivoParadaAoVivo.ServicoParando, t.Motivo);
        Assert.Equal(1, _fonte.Encerradas);
    }

    [Fact]
    public async Task SessaoQueSomeDaConfiguracaoEParada()
    {
        _mono.AvancaNoAtraso = false;
        var ger = NovoGerenciador();
        var sessao = Sessao();
        var cam = Cam();

        var r1 = await ger.ReconciliarAsync(new[] { sessao }, Cameras(cam));
        var t = ger.Transmissao(sessao.SessaoId)!;
        await Esperar(() => _fonte.QuantasAberturas == 1);
        var r2 = await ger.ReconciliarAsync(Array.Empty<SessaoAoVivoServidor>(), Cameras(cam));

        Assert.Equal(new[] { sessao.SessaoId.ToString("D") }, r1.Iniciadas);
        Assert.Equal(new[] { sessao.SessaoId.ToString("D") }, r2.Paradas);
        Assert.Equal(MotivoParadaAoVivo.SumiuDaConfiguracao, t.Motivo);
        Assert.True(t.Terminou);
        Assert.Equal(1, _fonte.Encerradas);
        Assert.Empty(ger.EmExecucao);
    }

    [Fact]
    public async Task SessaoMantidaNaoReiniciaEMudancaDeFormatoReinicia()
    {
        _mono.AvancaNoAtraso = false;
        var ger = NovoGerenciador();
        var id = Guid.NewGuid();
        var cam = Cam();

        await ger.ReconciliarAsync(new[] { Sessao(id: id, fps: 2) }, Cameras(cam));
        var r2 = await ger.ReconciliarAsync(new[] { Sessao(id: id, fps: 2, segundos: 590) }, Cameras(cam));
        var r3 = await ger.ReconciliarAsync(new[] { Sessao(id: id, fps: 4) }, Cameras(cam));

        Assert.Equal(new[] { id.ToString("D") }, r2.Mantidas);
        Assert.Equal(new[] { id.ToString("D") }, r3.Reiniciadas);
        Assert.Equal(2, _fonte.QuantasAberturas);
        Assert.Equal(4, _fonte.Aberturas[1].Perfil.Fps);
        await ger.ReconciliarAsync(Array.Empty<SessaoAoVivoServidor>(), Cameras(cam));
    }

    [Fact]
    public async Task TetoDe2TransmissoesSimultaneas_AsSessoesAlemDoTetoSaoIgnoradasComAviso()
    {
        _mono.AvancaNoAtraso = false;
        var log = new LogColetor();
        var ger = new GerenciadorAoVivo((s, c) => NovaTransmissao(s, c), CancellationToken.None, log, TimeSpan.FromSeconds(5));
        var cam = Cam();
        var s1 = Sessao();
        var s2 = Sessao();
        var s3 = Sessao();

        var r1 = await ger.ReconciliarAsync(new[] { s1, s2, s3 }, Cameras(cam));

        Assert.Equal(2, GerenciadorAoVivo.MaximoTransmissoesSimultaneas);
        Assert.Equal(new[] { s1.SessaoId.ToString("D"), s2.SessaoId.ToString("D") }, r1.Iniciadas);
        Assert.Equal(2, ger.EmExecucao.Count);
        Assert.Null(ger.Transmissao(s3.SessaoId));
        Assert.Contains(log.Linhas, l => l.Nivel == LogLevel.Warning && l.Texto.Contains(s3.SessaoId.ToString()) && l.Texto.Contains("ignorada"));

        // Uma sessão em curso não é trocada por outra que apareça antes dela na lista.
        var s4 = Sessao();
        var r2 = await ger.ReconciliarAsync(new[] { s4, s1, s2 }, Cameras(cam));
        Assert.Empty(r2.Iniciadas);
        Assert.Empty(r2.Paradas);
        Assert.Null(ger.Transmissao(s4.SessaoId));
        Assert.NotNull(ger.Transmissao(s1.SessaoId));
        Assert.NotNull(ger.Transmissao(s2.SessaoId));

        await ger.ReconciliarAsync(Array.Empty<SessaoAoVivoServidor>(), Cameras(cam));
    }

    [Fact]
    public async Task SessaoDeCameraQueNaoEstaAtivaEhIgnorada()
    {
        var ger = NovoGerenciador();
        var r = await ger.ReconciliarAsync(new[] { Sessao() }, Cameras());
        Assert.Empty(r.Iniciadas);
        Assert.Empty(ger.EmExecucao);
        Assert.Equal(0, _fonte.QuantasAberturas);
    }

    // ---------------------------------------------------------------- isolamento da análise e da fila

    [Fact]
    public async Task FalhaNoAoVivoNaoReiniciaNemAfetaOMonitorCameraDaAnalise()
    {
        // Análise real (MonitorCamera) rodando na mesma câmera, com a própria fonte.
        _relogio.AvancaNoAtraso = false;
        _mono.AvancaNoAtraso = false;
        var fila = new FilaQuadros(_pasta.Caminho, new ProtetorFalso(), _relogio, new LogColetor<FilaQuadros>(), TimeSpan.FromHours(24), 500L << 20);
        var analise = Cam(stream: 0);
        var clienteAnalise = new ClienteFalso();
        var enviador = new EnviadorCamera(analise, clienteAnalise, fila, new EstadoApi(), _relogio, _log);
        var fonteAnalise = new FonteFalsa { Padrao = ct => UmQuadroEPendura(ct) };
        var monitor = new MonitorCamera(analise, fonteAnalise, new AmostradorFalso(), enviador, _relogio, _mascarador, _log);
        using var ctsAnalise = new CancellationTokenSource();
        var tarefaAnalise = Task.Run(() => monitor.ExecutarAsync(ctsAnalise.Token));
        await Esperar(() => fonteAnalise.Chamadas == 1);

        // O ao vivo falha ao abrir a câmera.
        _fonte.Comportamento = FonteAoVivoFalsa.Falha("Server returned 401 Unauthorized");
        var ger = NovoGerenciador();
        var sessao = Sessao();
        await ger.ReconciliarAsync(new[] { sessao }, Cameras(analise));
        await Esperar(() => _cliente.QuantasFalhas == 1);
        await Esperar(() => ger.Transmissao(sessao.SessaoId)!.Terminou);
        await ger.ReconciliarAsync(new[] { sessao }, Cameras(analise)); // a sessão segue na lista da API
        await ger.ReconciliarAsync(new[] { sessao }, Cameras(analise));

        Assert.Equal(ClassificadorFalhaConexao.CodSenhaRecusada, _cliente.Falhas[0].Codigo);
        Assert.Equal(MotivoParadaAoVivo.Falha, ger.Transmissao(sessao.SessaoId)!.Motivo);
        Assert.Equal(1, _fonte.QuantasAberturas);   // o ao vivo NÃO se reabre sozinho
        Assert.Equal(1, _cliente.QuantasFalhas);
        Assert.Equal(1, fonteAnalise.Chamadas);     // a análise não foi reaberta...
        Assert.Equal(1, monitor.Sessoes);           // ...nem reiniciada
        Assert.False(tarefaAnalise.IsCompleted);

        ctsAnalise.Cancel();
        await tarefaAnalise.WaitAsync(TimeSpan.FromSeconds(5));
        await ger.ReconciliarAsync(Array.Empty<SessaoAoVivoServidor>(), Cameras(analise));
    }

    private static async IAsyncEnumerable<byte[]> UmQuadroEPendura([EnumeratorCancellation] CancellationToken ct)
    {
        yield return FonteAoVivoFalsa.Jpeg;
        await Task.Delay(Timeout.Infinite, ct);
    }

    [Fact]
    public async Task NadaEhGravadoNaFilaQuadros()
    {
        var fila = new FilaQuadros(_pasta.Caminho, new ProtetorFalso(), _relogio, new LogColetor<FilaQuadros>(), TimeSpan.FromHours(24), 500L << 20);
        var antes = Directory.GetFiles(_pasta.Caminho, "*", SearchOption.AllDirectories).Length;
        _cliente.PadraoQuandoVazio = ClienteAoVivoFalso.SemContato(); // o cenário em que a análise enfileiraria em disco
        _fonte.Comportamento = Quadros(30);
        var t = NovaTransmissao(Sessao(segundos: 5));

        await Task.Run(() => t.ExecutarAsync(CancellationToken.None)).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(0, fila.Contar(IdCam.ToString("D")));
        Assert.Equal(0, fila.TamanhoTotalBytes());
        Assert.Equal(antes, Directory.GetFiles(_pasta.Caminho, "*", SearchOption.AllDirectories).Length);
        // Estrutural: nenhuma peça do ao vivo recebe a fila.
        foreach (var tipo in new[] { typeof(TransmissaoAoVivo), typeof(EnviadorAoVivo), typeof(GerenciadorAoVivo), typeof(ClienteAoVivo) })
        {
            Assert.DoesNotContain(tipo.GetConstructors().SelectMany(c => c.GetParameters()), p => p.ParameterType == typeof(FilaQuadros));
            Assert.DoesNotContain(tipo.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public), f => f.FieldType == typeof(FilaQuadros));
        }
    }

    private static Func<CancellationToken, IAsyncEnumerable<byte[]>> Quadros(int n) => ct => GerarQuadros(n, ct);

    private static async IAsyncEnumerable<byte[]> GerarQuadros(int n, [EnumeratorCancellation] CancellationToken ct)
    {
        for (var i = 0; i < n; i++)
        {
            yield return FonteAoVivoFalsa.Jpeg;
            await Task.Yield();
        }
        await Task.Delay(Timeout.Infinite, ct);
    }

    // ---------------------------------------------------------------- segredos

    [Fact]
    public async Task SenhaNaoApareceEmLogNemNoMotivoDaFalha()
    {
        // Mascarador SEM a senha registrada: a transmissão por si só precisa sanear a mensagem da fonte.
        _mono.AvancaNoAtraso = false;
        var url = $"rtsp://admin:{Uri.EscapeDataString(Senha)}@192.168.0.8:554/user=admin&password={Uri.EscapeDataString(Senha)}&channel=1&stream=1.sdp?real_stream";
        _fonte.Comportamento = FonteAoVivoFalsa.Falha($"O ffmpeg encerrou (código 1). Saída: Error opening input {url}: Server returned 401 Unauthorized (authorization failed) senha={Senha}");
        var t = NovaTransmissao(Sessao());

        await Task.Run(() => t.ExecutarAsync(CancellationToken.None)).WaitAsync(TimeSpan.FromSeconds(10));

        var falha = Assert.Single(_cliente.Falhas);
        Assert.Equal(ClassificadorFalhaConexao.CodSenhaRecusada, falha.Codigo);
        var tudo = _log.Tudo() + "\n" + falha.Motivo + "\n" + falha.Codigo + "\n" + t.Sessao + "\n" + t.Enviador;
        Assert.DoesNotContain(Senha, tudo);
        Assert.DoesNotContain(Uri.EscapeDataString(Senha), tudo);
        Assert.DoesNotContain("rtsp://", tudo);
        Assert.Contains("Senha recusada", falha.Motivo);
    }

    [Fact]
    public void ToStringDaSessaoNaoTrazDadoSensivel()
    {
        var s = Sessao();
        var texto = s.ToString();
        Assert.Contains(s.SessaoId.ToString(), texto);
        Assert.DoesNotContain("Senha", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Segredo", texto, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------- envio: só o quadro mais recente

    [Fact]
    public async Task EnviadorGuardaSoOQuadroMaisRecenteEEnviaUmPorVez()
    {
        var porta = new TaskCompletionSource();
        _cliente.Bloqueio = () => porta.Task;
        var enviador = new EnviadorAoVivo(Guid.NewGuid(), _cliente, _mono, _mascarador, _log, _ => { });
        using var cts = new CancellationTokenSource();
        var tarefa = Task.Run(() => enviador.ExecutarAsync(cts.Token));

        var agora = DateTimeOffset.UtcNow;
        enviador.Oferecer(new byte[] { 1 }, agora);
        await Esperar(() => _cliente.QuadrosEnviados == 1); // o 1º está em voo (rede lenta)
        enviador.Oferecer(new byte[] { 2 }, agora);
        enviador.Oferecer(new byte[] { 3 }, agora);
        enviador.Oferecer(new byte[] { 4 }, agora);
        porta.SetResult();
        await Esperar(() => _cliente.QuadrosEnviados == 2);
        await Task.Delay(100);

        Assert.Equal(2, _cliente.QuadrosEnviados);
        Assert.Equal(new byte[] { 4 }, _cliente.Quadros[1].Jpeg);   // 2 e 3 foram descartados
        Assert.Equal(2, enviador.Descartados);
        Assert.Equal(1, _cliente.MaximoSimultaneo);
        cts.Cancel();
        await tarefa.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task QuadroMaiorQueOLimiteEDescartado()
    {
        var enviador = new EnviadorAoVivo(Guid.NewGuid(), _cliente, _mono, _mascarador, _log, _ => { });
        using var cts = new CancellationTokenSource();
        var tarefa = Task.Run(() => enviador.ExecutarAsync(cts.Token));

        enviador.Oferecer(new byte[EnviadorAoVivo.LimiteBytes + 1], DateTimeOffset.UtcNow);
        await Task.Delay(100);

        Assert.Equal(0, _cliente.QuadrosEnviados);
        Assert.Equal(1, enviador.Descartados);
        cts.Cancel();
        await tarefa.WaitAsync(TimeSpan.FromSeconds(5));
    }

    // ---------------------------------------------------------------- configuração do servidor

    [Fact]
    public void CampoAoVivoAusenteViraListaVaziaEPresenteEhLido()
    {
        var sem = ClienteConfiguracao.Desserializar("""{"intervaloConsultaSegundos":15,"cameras":[],"testesConexao":[]}""")!;
        Assert.Null(sem.AoVivo);

        var sessao = Guid.NewGuid();
        var cam = Guid.NewGuid();
        var com = ClienteConfiguracao.Desserializar($$"""
            {"intervaloConsultaSegundos":15,"cameras":[],"testesConexao":[],
             "aoVivo":[{"sessaoId":"{{sessao}}","cameraId":"{{cam}}","fps":2,"larguraMaxima":640,"segundosRestantes":540}]}
            """)!;
        var s = Assert.Single(com.AoVivo!);
        Assert.Equal(new SessaoAoVivoServidor(sessao, cam, 2, 640, 540), s);
    }

    [Fact]
    public async Task SincronizadorRepassaAoVivoAoGerenciadorEAusenciaDoCampoParaAsSessoes()
    {
        _mono.AvancaNoAtraso = false;
        var cliente = new ClienteConfigFalso();
        var executadas = new List<string>();
        var rec = new ReconciliadorCameras(async (c, ct) =>
        {
            lock (executadas) executadas.Add(c.CameraId);
            await Task.Delay(Timeout.Infinite, ct);
        }, CancellationToken.None, NullLogger.Instance, TimeSpan.FromSeconds(5));
        var ger = NovoGerenciador();
        var s = new SincronizadorConfiguracao(cliente, rec, null, null, Array.Empty<CameraConfig>(), new EstadoApi(), _relogio, _mascarador, _log, ger);
        var camServidor = new CameraServidor(IdCam, "Portaria", Guid.NewGuid(), "192.168.0.8", 554, "admin", Senha, 1, 0, 3);
        var sessao = Sessao();

        cliente.Enfileirar(new ResultadoConsulta(TipoConsulta.Sucesso, 200,
            new ConfiguracaoServidor(15, new List<CameraServidor> { camServidor }, new List<TesteServidor>(), new List<SessaoAoVivoServidor> { sessao }), null, null));
        await s.PassoAsync(CancellationToken.None);
        await Esperar(() => _fonte.QuantasAberturas == 1);

        Assert.Equal(new[] { sessao.SessaoId }, ger.EmExecucao.ToArray());
        Assert.Equal(1, _fonte.Aberturas[0].Camera.Stream);                 // a câmera da análise está no stream 0
        Assert.Equal(new[] { IdCam.ToString("D") }, rec.EmExecucao.Keys.ToArray());
        var t = ger.Transmissao(sessao.SessaoId)!;

        // API antiga / sessão encerrada: campo ausente = lista vazia. A análise segue intacta.
        cliente.Enfileirar(ClienteConfigFalso.Ok(new[] { camServidor }));
        await s.PassoAsync(CancellationToken.None);

        Assert.Empty(ger.EmExecucao);
        Assert.Equal(MotivoParadaAoVivo.SumiuDaConfiguracao, t.Motivo);
        Assert.Equal(new[] { IdCam.ToString("D") }, rec.EmExecucao.Keys.ToArray());
        Assert.Single(executadas);                                           // a análise não foi reiniciada
        await rec.ReconciliarAsync(Array.Empty<CameraConfig>());
    }

    [Fact]
    public void VersaoDoAgenteEhNoMinimo110()
    {
        Assert.True(Version.Parse(ClienteQuadros.VersaoDoAgente) >= new Version(1, 1, 0), ClienteQuadros.VersaoDoAgente);
    }
}

public class ClienteAoVivoTests
{
    private sealed class Handler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Resposta { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.NoContent);
        public HttpRequestMessage? Ultima { get; private set; }
        public byte[]? Corpo { get; private set; }
        public string? CorpoTexto => Corpo is null ? null : Encoding.UTF8.GetString(Corpo);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Ultima = request;
            Corpo = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(ct);
            return Resposta(request);
        }
    }

    private const string Agente = "3f2504e0-4f89-11d3-9a0c-0305e82c3301";
    private static readonly Guid Sessao = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private static (ClienteAoVivo, Handler) Criar(Mascarador? m = null)
    {
        var h = new Handler();
        return (new ClienteAoVivo(new HttpClient(h) { Timeout = Timeout.InfiniteTimeSpan }, new Uri("https://api.exemplo.com.br/sst"),
            Agente, "SEGREDO-123", m ?? new Mascarador()), h);
    }

    private static HttpResponseMessage Json(HttpStatusCode s, string json) =>
        new(s) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task QuadroUsaRotaCabecalhosECorpoJpegCru()
    {
        var (cliente, h) = Criar();
        var jpeg = new byte[] { 0xFF, 0xD8, 9, 8, 7, 0xFF, 0xD9 };
        var em = new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.FromHours(-3));

        var r = await cliente.EnviarQuadroAsync(Sessao, jpeg, em, CancellationToken.None);

        Assert.Equal(TipoResultadoAoVivo.Continuar, r.Tipo);
        Assert.Equal($"https://api.exemplo.com.br/sst/api/agentes-camera/{Agente}/ao-vivo/{Sessao:D}/quadros", h.Ultima!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Post, h.Ultima.Method);
        Assert.Equal("SEGREDO-123", h.Ultima.Headers.GetValues("X-Agente-Segredo").Single());
        Assert.Equal(ClienteQuadros.VersaoDoAgente, h.Ultima.Headers.GetValues("X-Agente-Versao").Single());
        Assert.Equal("image/jpeg", h.Ultima.Content!.Headers.ContentType!.MediaType);
        Assert.Equal(jpeg, h.Corpo);
        Assert.Equal(em.UtcDateTime, DateTimeOffset.Parse(h.Ultima.Headers.GetValues("X-Capturado-Em").Single()).UtcDateTime);
    }

    [Theory]
    [InlineData(410, TipoResultadoAoVivo.Encerrada)]
    [InlineData(401, TipoResultadoAoVivo.SegredoInvalido)]
    [InlineData(413, TipoResultadoAoVivo.Descartado)]
    [InlineData(400, TipoResultadoAoVivo.Descartado)]
    [InlineData(429, TipoResultadoAoVivo.LimiteTaxa)]
    [InlineData(500, TipoResultadoAoVivo.SemContato)]
    [InlineData(503, TipoResultadoAoVivo.SemContato)]
    [InlineData(409, TipoResultadoAoVivo.SemContato)]
    [InlineData(204, TipoResultadoAoVivo.Continuar)]
    public async Task StatusDaApiViraOResultadoCerto(int status, TipoResultadoAoVivo esperado)
    {
        var (cliente, h) = Criar();
        h.Resposta = _ => new HttpResponseMessage((HttpStatusCode)status);
        var r = await cliente.EnviarQuadroAsync(Sessao, new byte[] { 1 }, DateTimeOffset.UtcNow, CancellationToken.None);
        Assert.Equal(esperado, r.Tipo);
    }

    [Fact]
    public async Task Status404SoEncerraQuandoTrazOCodigoDeSessaoNaoEncontrada()
    {
        var (cliente, h) = Criar();
        h.Resposta = _ => new HttpResponseMessage(HttpStatusCode.NotFound);
        Assert.Equal(TipoResultadoAoVivo.SemContato, (await cliente.EnviarQuadroAsync(Sessao, new byte[] { 1 }, DateTimeOffset.UtcNow, CancellationToken.None)).Tipo);

        h.Resposta = _ => Json(HttpStatusCode.NotFound, """{"codigo":"sessao_nao_encontrada"}""");
        Assert.Equal(TipoResultadoAoVivo.Encerrada, (await cliente.EnviarQuadroAsync(Sessao, new byte[] { 1 }, DateTimeOffset.UtcNow, CancellationToken.None)).Tipo);
    }

    [Fact]
    public async Task Status429LeORetryAfter()
    {
        var (cliente, h) = Criar();
        h.Resposta = _ =>
        {
            var r = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            r.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(7));
            return r;
        };
        var res = await cliente.EnviarQuadroAsync(Sessao, new byte[] { 1 }, DateTimeOffset.UtcNow, CancellationToken.None);
        Assert.Equal(TimeSpan.FromSeconds(7), res.EsperarPor);
    }

    [Fact]
    public async Task ErroDeRedeViraSemContatoSemVazarSegredo()
    {
        var m = new Mascarador();
        m.Registrar("SEGREDO-123");
        var h = new Handler { Resposta = _ => throw new HttpRequestException("falha com SEGREDO-123 no meio") };
        var cliente = new ClienteAoVivo(new HttpClient(h), new Uri("https://api.exemplo.com.br"), Agente, "SEGREDO-123", m);

        var r = await cliente.EnviarQuadroAsync(Sessao, new byte[] { 1 }, DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Equal(TipoResultadoAoVivo.SemContato, r.Tipo);
        Assert.DoesNotContain("SEGREDO-123", r.Detalhe);
    }

    [Fact]
    public async Task FalhaVaiParaRotaFalhaComCodigoEMotivo()
    {
        var (cliente, h) = Criar();

        var ok = await cliente.EnviarFalhaAsync(Sessao, "senha_recusada", "Senha recusada pela câmera", CancellationToken.None);

        Assert.True(ok);
        Assert.Equal($"https://api.exemplo.com.br/sst/api/agentes-camera/{Agente}/ao-vivo/{Sessao:D}/falha", h.Ultima!.RequestUri!.ToString());
        Assert.Equal("SEGREDO-123", h.Ultima.Headers.GetValues("X-Agente-Segredo").Single());
        Assert.Equal(ClienteQuadros.VersaoDoAgente, h.Ultima.Headers.GetValues("X-Agente-Versao").Single());
        using var doc = System.Text.Json.JsonDocument.Parse(h.CorpoTexto!);
        Assert.Equal("senha_recusada", doc.RootElement.GetProperty("codigo").GetString());
        Assert.Equal("Senha recusada pela câmera", doc.RootElement.GetProperty("motivo").GetString());

        h.Resposta = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);
        Assert.False(await cliente.EnviarFalhaAsync(Sessao, "x", "y", CancellationToken.None));
    }
}

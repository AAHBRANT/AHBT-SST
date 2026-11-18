using System.Runtime.CompilerServices;
using AAHBRANT.SST.AgenteCameras.Envio;
using AAHBRANT.SST.AgenteCameras.Fila;
using AAHBRANT.SST.AgenteCameras.Quadros;
using AAHBRANT.SST.AgenteCameras.Seguranca;

namespace AAHBRANT.SST.AgenteCameras.Tests;

public class MonitorCameraTests : IDisposable
{
    private readonly PastaTemporaria _pasta = new();
    private readonly RelogioFalso _relogio = new();
    private readonly FonteFalsa _fonte = new();
    private readonly AmostradorFalso _amostrador = new();
    private readonly ClienteFalso _cliente = new();
    private readonly LogColetor _log = new();
    private readonly Mascarador _mascarador = new();
    private readonly EnviadorCamera _enviador;
    private readonly MonitorCamera _monitor;
    private static readonly byte[] Jpeg = { 0xFF, 0xD8, 1, 0xFF, 0xD9 };

    public MonitorCameraTests()
    {
        var fila = new FilaQuadros(_pasta.Caminho, new ProtetorFalso(), _relogio, new LogColetor<FilaQuadros>(), TimeSpan.FromHours(24), 500L << 20);
        _enviador = new EnviadorCamera(Modelos.Camera(), _cliente, fila, new EstadoApi(), _relogio, _log);
        _mascarador.Registrar("s3nh@Secreta");
        _monitor = new MonitorCamera(Modelos.Camera(), _fonte, _amostrador, _enviador, _relogio, _mascarador, _log);
    }

    public void Dispose() => _pasta.Dispose();

    private async Task RodarAteAsync(Func<bool> condicao, int segundos = 10)
    {
        using var cts = new CancellationTokenSource();
        var tarefa = Task.Run(() => _monitor.ExecutarAsync(cts.Token));
        var limite = DateTime.UtcNow.AddSeconds(segundos);
        while (!condicao() && DateTime.UtcNow < limite) await Task.Delay(10);
        var atingiu = condicao();
        cts.Cancel();
        await tarefa.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(atingiu, "condição não atingida a tempo");
    }

    private static Func<CancellationToken, IAsyncEnumerable<byte[]>> UmQuadroEDepoisPenduradaOuTermina(bool terminar) => ct => Gerar(terminar, ct);

    private static async IAsyncEnumerable<byte[]> Gerar(bool terminar, [EnumeratorCancellation] CancellationToken ct)
    {
        yield return Jpeg;
        if (!terminar) await Task.Delay(Timeout.Infinite, ct);
    }

    [Fact]
    public async Task EsperaCrescenteNasFalhasConsecutivas_2_4_8_16_32_60_60()
    {
        for (var i = 0; i < 8; i++) _fonte.Enfileirar(FonteFalsa.Falha("conexão recusada"));
        await RodarAteAsync(() => _fonte.Chamadas >= 8);

        var esperas = _relogio.Atrasos.Where(a => a >= TimeSpan.FromSeconds(2)).Take(7).Select(a => (int)a.TotalSeconds).ToArray();
        Assert.Equal(new[] { 2, 4, 8, 16, 32, 60, 60 }, esperas);
    }

    [Fact]
    public async Task NuncaDesisteMesmoDepoisDeMuitasFalhas()
    {
        for (var i = 0; i < 40; i++) _fonte.Enfileirar(FonteFalsa.Falha("câmera fora do ar"));
        await RodarAteAsync(() => _fonte.Chamadas >= 41);
        Assert.True(_fonte.Chamadas >= 41);
    }

    [Fact]
    public async Task SessaoQueEntregouQuadrosRecomecaAEscalaDoZero()
    {
        _fonte.Enfileirar(FonteFalsa.Falha("a"));
        _fonte.Enfileirar(FonteFalsa.Falha("b"));
        _fonte.Enfileirar(UmQuadroEDepoisPenduradaOuTermina(terminar: true)); // entrega e cai
        _fonte.Enfileirar(FonteFalsa.Falha("c"));
        await RodarAteAsync(() => _fonte.Chamadas >= 5);

        var esperas = _relogio.Atrasos.Where(a => a >= TimeSpan.FromSeconds(2)).Take(4).Select(a => (int)a.TotalSeconds).ToArray();
        // 2 s, 4 s; a sessão que entregou quadro recomeça em 2 s; a falha seguinte sem quadros sobe para 4 s
        Assert.Equal(new[] { 2, 4, 2, 4 }, esperas);
    }

    [Fact]
    public async Task MaisDe20SegundosSemQuadroEncerraEReligaComEspera()
    {
        _fonte.Enfileirar(UmQuadroEDepoisPenduradaOuTermina(terminar: false)); // entrega 1 e trava
        await RodarAteAsync(() => _fonte.Chamadas >= 2);

        Assert.Contains(_log.Linhas, l => l.Texto.Contains("sem quadros há mais de 20 s"));
        Assert.Contains(TimeSpan.FromSeconds(2), _relogio.Atrasos);
    }

    [Fact]
    public async Task ReligaACada30MinutosMesmoComOFluxoSaudavel()
    {
        // fonte saudável: entrega um quadro por segundo (de relógio falso) para sempre
        _relogio.AvancaNoAtraso = false; // só a fonte avança o tempo, junto com cada quadro
        _fonte.Padrao = ct => Saudavel(ct);
        await RodarAteAsync(() => _fonte.Chamadas >= 2, segundos: 20);

        Assert.Contains(_log.Linhas, l => l.Texto.Contains("renovação periódica"));
        Assert.True(_relogio.Agora - new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.FromHours(-3)) >= TimeSpan.FromMinutes(30));
    }

    private async IAsyncEnumerable<byte[]> Saudavel([EnumeratorCancellation] CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
            _relogio.Avancar(TimeSpan.FromSeconds(1));
            yield return Jpeg;
        }
    }

    [Fact]
    public async Task FfmpegAusenteLogaErroClaroETentaDeNovoEm1Minuto()
    {
        _fonte.Enfileirar(ct => throw new FfmpegIndisponivelException("ffmpeg não encontrado em \"C:\\x\\ffmpeg.exe\"."));
        _fonte.Enfileirar(ct => throw new FfmpegIndisponivelException("ffmpeg não encontrado em \"C:\\x\\ffmpeg.exe\"."));
        await RodarAteAsync(() => _fonte.Chamadas >= 3);

        Assert.True(_log.Linhas.Count(l => l.Nivel == Microsoft.Extensions.Logging.LogLevel.Error && l.Texto.Contains("ffmpeg não encontrado")) >= 2);
        var esperas = _relogio.Atrasos.Where(a => a >= TimeSpan.FromSeconds(2)).Take(2).ToArray();
        Assert.Equal(new[] { MonitorCamera.EsperaFfmpegAusente, MonitorCamera.EsperaFfmpegAusente }, esperas);
    }

    [Fact]
    public async Task QuadrosSemMovimentoNaoSaemEComMovimentoSaem()
    {
        var contador = 0;
        _amostrador.Regra = _ =>
        {
            var n = Interlocked.Increment(ref contador);
            var a = new byte[DetectorMovimento.LarguraAmostra * DetectorMovimento.AlturaAmostra];
            // 1-3 aquecimento/referência; 4-6 iguais; 7 muda
            if (n >= 7) for (var i = 0; i < 300; i++) a[i] = 255;
            return a;
        };
        _fonte.Enfileirar(ct => Varios(10, ct));
        await RodarAteAsync(() => Volatile.Read(ref contador) >= 10);

        // Só o quadro 7 (a mudança) foi entregue ao enviador; os demais, sem movimento, não.
        Assert.Null(await _enviador.PassoAsync(CancellationToken.None));
        Assert.Single(_cliente.Chamadas);
        Assert.NotNull(await _enviador.PassoAsync(CancellationToken.None)); // nada mais pendente
        Assert.Single(_cliente.Chamadas);
    }

    private static async IAsyncEnumerable<byte[]> Varios(int n, [EnumeratorCancellation] CancellationToken ct)
    {
        for (var i = 0; i < n; i++)
        {
            await Task.Yield();
            yield return Jpeg;
        }
        await Task.Delay(Timeout.Infinite, ct);
    }

    [Fact]
    public async Task MensagemDeErroDaFonteNaoVazaSenhaNemUrl()
    {
        var url = "rtsp://192.168.0.8:554/user=admin&password=s3nh%40Secreta&channel=1&stream=1.sdp?real_stream";
        _fonte.Enfileirar(FonteFalsa.Falha($"Falha ao abrir {url} (senha s3nh@Secreta)"));
        await RodarAteAsync(() => _fonte.Chamadas >= 2);
        var tudo = _log.Tudo();
        Assert.DoesNotContain("s3nh", tudo);
        Assert.Contains("password=***", tudo);
    }

    [Fact]
    public async Task CameraPausadaPor404ContinuaComOMonitorVivoEDescartaOsQuadros()
    {
        var fila = new FilaQuadros(_pasta.Caminho + "-x", new ProtetorFalso(), _relogio, new LogColetor<FilaQuadros>(), TimeSpan.FromHours(24), 500L << 20);
        var enviador = new EnviadorCamera(Modelos.Camera(), _cliente, fila, new EstadoApi(), _relogio, _log);
        var monitor = new MonitorCamera(Modelos.Camera(), _fonte, _amostrador, enviador, _relogio, _mascarador, _log);
        _cliente.Enfileirar(ClienteFalso.Status(TipoResultado.CameraNaoPertence, 404));
        _fonte.Padrao = ct => Varios(1000, ct);
        _amostrador.Regra = _ => new byte[2304];

        enviador.OferecerVivo(Jpeg, _relogio.Agora);
        await enviador.PassoAsync(CancellationToken.None);
        Assert.True(enviador.CameraPausadaAte > _relogio.Agora);

        // O monitor NAO encerra: segue abrindo o fluxo ate a parada do servico.
        using var cts = new CancellationTokenSource();
        var tarefa = Task.Run(() => monitor.ExecutarAsync(cts.Token));
        var limite = DateTime.UtcNow.AddSeconds(5);
        while (_fonte.Chamadas == 0 && DateTime.UtcNow < limite) await Task.Delay(10);
        Assert.False(tarefa.IsCompleted);
        Assert.True(_fonte.Chamadas > 0);
        cts.Cancel();
        await tarefa.WaitAsync(TimeSpan.FromSeconds(5));
        Directory.Delete(_pasta.Caminho + "-x", true);
    }
}

using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using AAHBRANT.SST.AgenteCameras.Envio;
using AAHBRANT.SST.AgenteCameras.Infra;
using AAHBRANT.SST.AgenteCameras.Opcoes;
using AAHBRANT.SST.AgenteCameras.Quadros;
using AAHBRANT.SST.AgenteCameras.Seguranca;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.AgenteCameras.Tests;

// Relógio controlável: AtrasarAsync avança o tempo na hora (sem esperar de verdade) e cede a vez.
public sealed class RelogioFalso : IRelogio
{
    private long _ticks;
    private readonly List<TimeSpan> _atrasos = new();

    public RelogioFalso(DateTimeOffset? inicio = null) => _ticks = (inicio ?? new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.FromHours(-3))).UtcTicks;

    // false: AtrasarAsync só cede a vez; quem avança o tempo é o teste (Avancar), de forma determinística.
    public bool AvancaNoAtraso { get; set; } = true;

    public DateTimeOffset Agora => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);
    public void Avancar(TimeSpan t) => Interlocked.Add(ref _ticks, t.Ticks);

    public IReadOnlyList<TimeSpan> Atrasos
    {
        get { lock (_atrasos) return _atrasos.ToList(); }
    }

    public async Task AtrasarAsync(TimeSpan tempo, CancellationToken ct)
    {
        lock (_atrasos) _atrasos.Add(tempo);
        ct.ThrowIfCancellationRequested();
        await Task.Yield();
        ct.ThrowIfCancellationRequested();
        if (AvancaNoAtraso) Avancar(tempo);
    }
}

// "Criptografia" reversível trivial para testes que não dependem do DPAPI.
public sealed class ProtetorFalso : IProtetorDados
{
    public byte[] Proteger(byte[] dados) => dados.Select(b => (byte)(b ^ 0x5A)).Prepend((byte)0xEE).ToArray();

    public byte[] Desproteger(byte[] dados)
    {
        if (dados.Length == 0 || dados[0] != 0xEE) throw new InvalidDataException("não protegido por este protetor");
        return dados.Skip(1).Select(b => (byte)(b ^ 0x5A)).ToArray();
    }
}

public sealed class LogColetor : ILogger
{
    public List<(LogLevel Nivel, string Texto)> Linhas { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (Linhas) Linhas.Add((logLevel, formatter(state, exception) + (exception is null ? "" : " " + exception.Message)));
    }

    public string Tudo()
    {
        lock (Linhas) return string.Join("\n", Linhas.Select(l => l.Texto));
    }
}

public sealed class LogColetor<T> : ILogger<T>
{
    private readonly LogColetor _interno = new();
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => _interno.Log(logLevel, eventId, state, exception, formatter);
    public string Tudo() => _interno.Tudo();
}

public sealed class ClienteFalso : IClienteQuadros
{
    private readonly Queue<ResultadoEnvio> _respostas = new();
    private int _simultaneos;

    public List<(string CameraId, byte[] Jpeg, DateTimeOffset Em)> Chamadas { get; } = new();
    public ResultadoEnvio PadraoQuandoVazio { get; set; } = Ok();
    public int MaximoSimultaneo { get; private set; }
    public Func<Task>? Bloqueio { get; set; }

    public static ResultadoEnvio Ok(string situacao = "ocorrencia") => new(TipoResultado.Sucesso, 200,
        new RespostaQuadro { Situacao = situacao, Pessoas = 2, SemCapacete = 1, SemColete = 0, AlertaEnviado = true, Repetida = false }, null, null);
    public static ResultadoEnvio Rede() => ResultadoEnvio.SemInternet("sem rede");
    public static ResultadoEnvio Status(TipoResultado tipo, int http, TimeSpan? espera = null) => new(tipo, http, null, espera, "x");

    public void Enfileirar(params ResultadoEnvio[] r)
    {
        foreach (var x in r) _respostas.Enqueue(x);
    }

    public async Task<ResultadoEnvio> EnviarAsync(string cameraId, byte[] jpeg, DateTimeOffset capturadaEm, CancellationToken ct)
    {
        var agora = Interlocked.Increment(ref _simultaneos);
        lock (Chamadas)
        {
            MaximoSimultaneo = Math.Max(MaximoSimultaneo, agora);
            Chamadas.Add((cameraId, jpeg, capturadaEm));
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
}

public sealed class AmostradorFalso : IAmostrador
{
    public Func<byte[], byte[]?> Regra { get; set; } = _ => new byte[DetectorMovimento.LarguraAmostra * DetectorMovimento.AlturaAmostra];
    public byte[]? Amostrar(byte[] jpeg) => Regra(jpeg);
}

public sealed class FonteFalsa : IFonteQuadros
{
    private readonly Queue<Func<CancellationToken, IAsyncEnumerable<byte[]>>> _sessoes = new();
    private int _chamadas;
    public int Chamadas => Volatile.Read(ref _chamadas);
    public Func<CancellationToken, IAsyncEnumerable<byte[]>>? Padrao { get; set; }

    public void Enfileirar(Func<CancellationToken, IAsyncEnumerable<byte[]>> sessao) => _sessoes.Enqueue(sessao);

    public IAsyncEnumerable<byte[]> LerQuadrosAsync(CameraConfig camera, CancellationToken ct)
    {
        Interlocked.Increment(ref _chamadas);
        Func<CancellationToken, IAsyncEnumerable<byte[]>>? s;
        lock (_sessoes) s = _sessoes.Count > 0 ? _sessoes.Dequeue() : Padrao;
        return (s ?? Pendurada)(ct);
    }

    public static async IAsyncEnumerable<byte[]> Pendurada([EnumeratorCancellation] CancellationToken ct)
    {
        await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
        yield break;
    }

    public static Func<CancellationToken, IAsyncEnumerable<byte[]>> Falha(string mensagem) => ct => FalhaImpl(mensagem);

    private static async IAsyncEnumerable<byte[]> FalhaImpl(string mensagem)
    {
        await Task.Yield();
        throw new InvalidOperationException(mensagem);
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }
}

public sealed class PastaTemporaria : IDisposable
{
    public string Caminho { get; } = Path.Combine(Path.GetTempPath(), "agcam-" + Guid.NewGuid().ToString("N"));

    public PastaTemporaria() => Directory.CreateDirectory(Caminho);

    public void Dispose()
    {
        try { Directory.Delete(Caminho, true); } catch (IOException) { }
    }
}

public static class Modelos
{
    public static CameraConfig Camera(string id = "cam-1", string nome = "Portaria", int intervaloSeg = 3, string senha = "s3nh@Secreta") =>
        new(id, nome, "192.168.0.8", 554, "admin", senha, 1, 1, null, TimeSpan.FromSeconds(intervaloSeg));

    public static byte[] JpegSintetico(Color cor, int largura = 320, int altura = 180)
    {
        using var bmp = new Bitmap(largura, altura);
        using (var g = Graphics.FromImage(bmp)) g.Clear(cor);
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Jpeg);
        return ms.ToArray();
    }
}

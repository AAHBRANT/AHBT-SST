using System.Drawing;
using System.Text;
using System.Text.Json;
using AAHBRANT.SST.AgenteCameras.AoVivo;
using AAHBRANT.SST.AgenteCameras.Envio;
using AAHBRANT.SST.AgenteCameras.Opcoes;
using AAHBRANT.SST.AgenteCameras.Quadros;
using AAHBRANT.SST.AgenteCameras.Seguranca;
using AAHBRANT.SST.AgenteCameras.Servidor;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace AAHBRANT.SST.AgenteCameras.Tests;

// "Parte da imagem" por câmera: 0 inteira, 1 metade de cima, 2 metade de baixo. O recorte é feito no ffmpeg.
public class ParteImagemTests : IDisposable
{
    private static readonly Guid IdA = Guid.Parse("aaaaaaaa-0000-0000-0000-00000000000a");
    private static readonly Guid IdB = Guid.Parse("bbbbbbbb-0000-0000-0000-00000000000b");
    private const string Senha = "Senha&Parte#1";

    private readonly ITestOutputHelper _saida;
    private readonly PastaTemporaria _pasta = new();
    private readonly LogColetor _log = new();

    public ParteImagemTests(ITestOutputHelper saida) => _saida = saida;

    public void Dispose() => _pasta.Dispose();

    private static CameraServidor CamServ(Guid id, int parte, string host = "192.168.0.8", int canal = 1, int stream = 0) =>
        new(id, "Cam " + parte, Guid.NewGuid(), host, 554, "admin", Senha, canal, stream, 3, parte);

    private static string Filtro(IReadOnlyList<string> args) => args[args.ToList().IndexOf("-vf") + 1];

    // ---------------------------------------------------------------- argumentos do ffmpeg

    [Fact]
    public void Parte0_ArgumentosIdenticosAosDeHoje()
    {
        var antigo = FfmpegFonteQuadros.MontarArgumentos("rtsp://x", 10);
        Assert.Equal(antigo, FfmpegFonteQuadros.MontarArgumentos("rtsp://x", 10, PerfilCaptura.Analise, 0));
        Assert.Equal(antigo, FfmpegFonteQuadros.MontarArgumentos("rtsp://x", 10, PerfilCaptura.Analise, ParteImagemCamera.Inteira));
        Assert.Contains("fps=1,scale='min(1280,iw)':-2", antigo);
        Assert.DoesNotContain(antigo, a => a.Contains("crop"));
    }

    [Theory]
    [InlineData(1, "crop=iw:ih/2:0:0")]
    [InlineData(2, "crop=iw:ih/2:0:ih/2")]
    public void Parte1e2_CropVemAntesDeFpsEScale(int parte, string crop)
    {
        var args = FfmpegFonteQuadros.MontarArgumentos("rtsp://x", 10, PerfilCaptura.Analise, parte);
        var filtro = Filtro(args);

        Assert.Equal($"{crop},fps=1,scale='min(1280,iw)':-2", filtro);
        Assert.True(filtro.IndexOf("crop", StringComparison.Ordinal) < filtro.IndexOf("fps", StringComparison.Ordinal));
        Assert.True(filtro.IndexOf("crop", StringComparison.Ordinal) < filtro.IndexOf("scale", StringComparison.Ordinal));

        // Fora o filtro, a linha é a mesma da imagem inteira.
        var inteira = FfmpegFonteQuadros.MontarArgumentos("rtsp://x", 10).ToList();
        var recortada = args.ToList();
        recortada[recortada.IndexOf("-vf") + 1] = inteira[inteira.IndexOf("-vf") + 1];
        Assert.Equal(inteira, recortada);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(99)]
    public void ParteForaDeRange_ValeInteira(int parte)
    {
        Assert.Equal(0, ParteImagemCamera.Normalizar(parte));
        Assert.Equal(FfmpegFonteQuadros.MontarArgumentos("rtsp://x", 10), FfmpegFonteQuadros.MontarArgumentos("rtsp://x", 10, PerfilCaptura.Analise, parte));
    }

    [Fact]
    public void ArgumentosDaAnaliseParte2_EAoVivoParte1_ComSenhaMascarada()
    {
        string Linha(CameraConfig c, PerfilCaptura p) =>
            "ffmpeg " + string.Join(" ", FfmpegFonteQuadros.MontarArgumentos(UrlRtsp.Montar(c), 10, p, c.ParteImagem)
                .Select(a => a.Replace(Uri.EscapeDataString(Senha), "***")));

        var analise = Modelos.Camera(IdA.ToString("D"), "A", senha: Senha) with { Stream = 0, ParteImagem = 2 };
        var aoVivo = Modelos.Camera(IdB.ToString("D"), "B", senha: Senha) with { Stream = 1, ParteImagem = 1 };
        var linhaAnalise = Linha(analise, PerfilCaptura.Analise);
        var linhaAoVivo = Linha(aoVivo, PerfilCaptura.AoVivo(2, 640));

        _saida.WriteLine("ARGS_ANALISE_PARTE2: " + linhaAnalise);
        _saida.WriteLine("ARGS_AOVIVO_PARTE1: " + linhaAoVivo);
        Assert.DoesNotContain(Uri.EscapeDataString(Senha), linhaAnalise);
        Assert.DoesNotContain(Uri.EscapeDataString(Senha), linhaAoVivo);
        Assert.Contains("crop=iw:ih/2:0:ih/2,fps=1,", linhaAnalise);
        Assert.Contains("crop=iw:ih/2:0:0,fps=2,scale='min(640,iw)':-2", linhaAoVivo);
    }

    // Ponta a ponta sem câmera: um "ffmpeg" falso (.cmd) grava os argumentos recebidos e sai com erro.
    [Fact]
    public async Task FonteReal_PassaAParteDaCameraAoFfmpeg()
    {
        var falso = Path.Combine(_pasta.Caminho, "ffmpeg.cmd");
        var saida = Path.Combine(_pasta.Caminho, "args.txt");
        File.WriteAllText(falso, "@echo off\r\necho %* > \"%~dp0args.txt\"\r\nexit /b 1\r\n");
        var fonte = new FfmpegFonteQuadros(falso, new Mascarador(), NullLogger<FfmpegFonteQuadros>.Instance);
        var cam = Modelos.Camera(IdA.ToString("D")) with { ModeloUrl = "rtsp://{host}:{porta}/x", ParteImagem = 2 };

        async Task Ler(CameraConfig c, bool aoVivo)
        {
            await Assert.ThrowsAsync<FonteEncerradaException>(async () =>
            {
                if (aoVivo)
                    await foreach (var _ in fonte.LerQuadrosAsync(c, PerfilCaptura.AoVivo(2, 640), CancellationToken.None)) { }
                else
                    await foreach (var _ in fonte.LerQuadrosAsync(c, CancellationToken.None)) { }
            });
        }

        await Ler(cam, aoVivo: false);
        Assert.Contains("crop=iw:ih/2:0:ih/2,fps=1,scale='min(1280,iw)':-2", File.ReadAllText(saida));

        File.Delete(saida);
        await Ler(cam with { ParteImagem = 1 }, aoVivo: true);
        Assert.Contains("crop=iw:ih/2:0:0,fps=2,scale='min(640,iw)':-2", File.ReadAllText(saida));

        File.Delete(saida);
        await Ler(cam with { ParteImagem = 0 }, aoVivo: false);
        Assert.DoesNotContain("crop", File.ReadAllText(saida));
    }

    // ---------------------------------------------------------------- modelos do servidor

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 0)]
    [InlineData(-5, 0)]
    public void ConversorLeAParteEIgnoraForaDeRange(int enviada, int esperada)
    {
        var c = ConversorServidor.Converter(CamServ(IdA, enviada), new Mascarador(), out var problema);
        Assert.Null(problema);
        Assert.Equal(esperada, c!.ParteImagem);
    }

    [Fact]
    public void JsonDaApi_ComEAusenteDoCampo()
    {
        var json = $$"""
        {"intervaloConsultaSegundos":60,
         "cameras":[
           {"cameraId":"{{IdA}}","host":"h","portaRtsp":554,"senha":"s","canal":1,"stream":0,"intervaloEnvioSegundos":3,"parteImagem":2},
           {"cameraId":"{{IdB}}","host":"h","portaRtsp":554,"senha":"s","canal":1,"stream":0,"intervaloEnvioSegundos":3}],
         "testesConexao":[
           {"testeId":"{{Guid.NewGuid()}}","host":"h","portaRtsp":554,"senha":"s","canal":1,"stream":0,"parteImagem":1},
           {"testeId":"{{Guid.NewGuid()}}","host":"h","portaRtsp":554,"senha":"s","canal":1,"stream":0}]}
        """;
        var cfg = ClienteConfiguracao.Desserializar(json)!;

        Assert.Equal(new[] { 2, 0 }, cfg.Cameras!.Select(c => c.ParteImagem).ToArray());
        Assert.Equal(new[] { 1, 0 }, cfg.TestesConexao!.Select(t => t.ParteImagem).ToArray());
    }

    // ---------------------------------------------------------------- reinício por mudança de parte

    private sealed class Painel
    {
        public readonly object Trava = new();
        public List<string> Iniciadas = new();
        public async Task Executar(CameraConfig c, CancellationToken ct)
        {
            lock (Trava) Iniciadas.Add(c.CameraId + ":" + c.ParteImagem);
            await Task.Delay(Timeout.Infinite, ct);
        }
        public string[] I() { lock (Trava) return Iniciadas.ToArray(); }
    }

    [Fact]
    public async Task MudarAParteReiniciaSoAquelaCamera()
    {
        var p = new Painel();
        var rec = new ReconciliadorCameras(p.Executar, CancellationToken.None, NullLogger.Instance, TimeSpan.FromSeconds(5));
        var a = Modelos.Camera("a");
        var b = Modelos.Camera("b");
        await rec.ReconciliarAsync(new[] { a, b });

        var mesma = await rec.ReconciliarAsync(new[] { a, b });
        var r = await rec.ReconciliarAsync(new[] { a with { ParteImagem = 1 }, b });

        Assert.False(mesma.HouveMudanca);
        Assert.Equal(new[] { "a" }, r.Reiniciadas);
        Assert.Equal(new[] { "b" }, r.Mantidas);
        Assert.Equal(1, rec.EmExecucao["a"].ParteImagem);
        Assert.Equal(new[] { "a:0", "b:0", "a:1" }, p.I());
        await rec.ReconciliarAsync(Array.Empty<CameraConfig>());
    }

    // ---------------------------------------------------------------- cache local

    private CacheCameras NovoCache() => new(_pasta.Caminho, new ProtetorFalso(), "agente-1", _log);

    [Fact]
    public void Cache_GuardaAParteELeCacheAntigoSemOCampoComoZero()
    {
        var cache = NovoCache();
        cache.Salvar(new[] { Modelos.Camera("a") with { ParteImagem = 2 }, Modelos.Camera("b") });
        var lidas = NovoCache().Carregar().ToDictionary(c => c.CameraId);
        Assert.Equal(2, lidas["a"].ParteImagem);
        Assert.Equal(0, lidas["b"].ParteImagem);

        // Cache da versão anterior, antes do campo existir: o JSON não tem "ParteImagem".
        var antigo = JsonSerializer.Serialize(new
        {
            AgenteId = "agente-1",
            Cameras = new[]
            {
                new
                {
                    CameraId = "c", Nome = "Antiga", Host = "192.168.0.8", PortaRtsp = 554, Usuario = "admin", Senha = "x",
                    Canal = 1, Stream = 1, ModeloUrl = (string?)null, IntervaloEnvio = "00:00:03",
                },
            },
        });
        Assert.DoesNotContain("ParteImagem", antigo);
        File.WriteAllBytes(cache.Caminho, new ProtetorFalso().Proteger(Encoding.UTF8.GetBytes(antigo)));

        var c = Assert.Single(NovoCache().Carregar());
        Assert.Equal("c", c.CameraId);
        Assert.Equal(0, c.ParteImagem);
    }

    // ---------------------------------------------------------------- teste de conexão

    [Theory]
    [InlineData(2, "crop=iw:ih/2:0:ih/2")]
    [InlineData(1, "crop=iw:ih/2:0:0")]
    [InlineData(0, null)]
    public async Task TesteDeConexao_CapturaComAParteDoPedido(int parte, string? crop)
    {
        var fonte = new FonteGravadora();
        var cliente = new ClienteConfigFalso();
        var proc = new ProcessadorTestesConexao(fonte, cliente, new Mascarador(), new RelogioFalso(), _log, CancellationToken.None,
            TimeSpan.FromSeconds(5), maximoSimultaneos: 2, pausaReenvio: TimeSpan.FromMilliseconds(1));

        proc.Aceitar(new[] { new TesteServidor(Guid.NewGuid(), Guid.NewGuid(), null, "192.168.0.8", 554, "admin", Senha, 1, 0, parte) });
        await proc.AguardarTodosAsync();

        var camera = Assert.Single(fonte.Cameras);
        Assert.Equal(parte, camera.ParteImagem);
        var filtro = Filtro(FfmpegFonteQuadros.MontarArgumentos(UrlRtsp.Montar(camera), 10, PerfilCaptura.Analise, camera.ParteImagem));
        if (crop is null) Assert.DoesNotContain("crop", filtro); else Assert.StartsWith(crop + ",fps=", filtro);
        Assert.True(Assert.Single(cliente.Resultados).Resultado.Sucesso);
    }

    private sealed class FonteGravadora : IFonteQuadros
    {
        public List<CameraConfig> Cameras { get; } = new();

        public IAsyncEnumerable<byte[]> LerQuadrosAsync(CameraConfig camera, CancellationToken ct)
        {
            lock (Cameras) Cameras.Add(camera);
            return Um(Modelos.JpegSintetico(Color.Gray), ct);
        }

        private static async IAsyncEnumerable<byte[]> Um(byte[] jpeg, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await Task.Yield();
            yield return jpeg;
            await Task.Delay(Timeout.Infinite, ct);
        }
    }

    // ---------------------------------------------------------------- ao vivo

    private static SessaoAoVivoServidor Sessao(Guid camera, Guid? id = null) => new(id ?? Guid.NewGuid(), camera, 2, 640, 600);

    private (GerenciadorAoVivo Ger, FonteAoVivoFalsa Fonte) NovoGerenciador()
    {
        var fonte = new FonteAoVivoFalsa();
        var mono = new RelogioMonotonicoFalso { AvancaNoAtraso = false };
        var masc = new Mascarador();
        var ger = new GerenciadorAoVivo(
            (s, c) => new TransmissaoAoVivo(s, c, fonte, new ClienteAoVivoFalso(), new RelogioFalso(), mono, masc, _log, null),
            CancellationToken.None, NullLogger.Instance, TimeSpan.FromSeconds(5));
        return (ger, fonte);
    }

    private static async Task Esperar(Func<bool> condicao)
    {
        var limite = DateTime.UtcNow.AddSeconds(10);
        while (!condicao() && DateTime.UtcNow < limite) await Task.Delay(10);
        Assert.True(condicao(), "condição não atingida a tempo");
    }

    private static IReadOnlyDictionary<string, CameraConfig> Cameras(params CameraConfig[] c) =>
        c.ToDictionary(x => x.CameraId, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public async Task AoVivo_UsaAParteDaCameraEMudancaDeParteReiniciaATransmissao()
    {
        var (ger, fonte) = NovoGerenciador();
        var cam = Modelos.Camera(IdA.ToString("D"), senha: Senha) with { ParteImagem = 1 };
        var sessao = Sessao(IdA);

        await ger.ReconciliarAsync(new[] { sessao }, Cameras(cam));
        await Esperar(() => fonte.QuantasAberturas == 1);
        Assert.Equal(1, fonte.Aberturas[0].Camera.ParteImagem);
        Assert.Equal(1, fonte.Aberturas[0].Camera.Stream);   // o ao vivo segue no stream secundário

        var mantida = await ger.ReconciliarAsync(new[] { sessao }, Cameras(cam));
        var reiniciada = await ger.ReconciliarAsync(new[] { sessao }, Cameras(cam with { ParteImagem = 2 }));
        await Esperar(() => fonte.QuantasAberturas == 2);

        Assert.Equal(new[] { sessao.SessaoId.ToString("D") }, mantida.Mantidas);
        Assert.Equal(new[] { sessao.SessaoId.ToString("D") }, reiniciada.Reiniciadas);
        Assert.Equal(2, fonte.Aberturas[1].Camera.ParteImagem);
        await ger.ReconciliarAsync(Array.Empty<SessaoAoVivoServidor>(), Cameras(cam));
    }

    // ---------------------------------------------------------------- duas câmeras, mesmo equipamento

    [Fact]
    public async Task DuasCamerasNoMesmoHostCanalStreamComPartesDiferentesConvivem()
    {
        var p = new Painel();
        var rec = new ReconciliadorCameras(p.Executar, CancellationToken.None, NullLogger.Instance, TimeSpan.FromSeconds(5));
        var cache = NovoCache();
        var cliente = new ClienteConfigFalso();
        var sinc = new SincronizadorConfiguracao(cliente, rec, null, cache, Array.Empty<CameraConfig>(), new EstadoApi(),
            new RelogioFalso(), new Mascarador(), _log);
        cliente.Enfileirar(ClienteConfigFalso.Ok(new[] { CamServ(IdA, 1), CamServ(IdB, 2) }));

        await sinc.PassoAsync(CancellationToken.None);

        Assert.Equal(2, rec.EmExecucao.Count);
        var a = rec.EmExecucao[IdA.ToString("D")];
        var b = rec.EmExecucao[IdB.ToString("D")];
        Assert.Equal(1, a.ParteImagem);
        Assert.Equal(2, b.ParteImagem);
        Assert.Equal((a.Host, a.Canal, a.Stream), (b.Host, b.Canal, b.Stream));
        Assert.Equal(UrlRtsp.Montar(a), UrlRtsp.Montar(b));

        // O cache guarda as duas, cada uma com a sua parte.
        var lidas = NovoCache().Carregar().ToDictionary(c => c.CameraId);
        Assert.Equal(1, lidas[IdA.ToString("D")].ParteImagem);
        Assert.Equal(2, lidas[IdB.ToString("D")].ParteImagem);

        // Mudar só a parte da B reinicia só a B.
        cliente.Enfileirar(ClienteConfigFalso.Ok(new[] { CamServ(IdA, 1), CamServ(IdB, 0) }));
        await sinc.PassoAsync(CancellationToken.None);
        Assert.Equal(new[] { IdA.ToString("D") + ":1", IdB.ToString("D") + ":2", IdB.ToString("D") + ":0" }, p.I());
        await rec.ReconciliarAsync(Array.Empty<CameraConfig>());
    }

    [Fact]
    public async Task AoVivoDeDuasCamerasDoMesmoEquipamentoAbreUmaLeituraPorCameraId()
    {
        var (ger, fonte) = NovoGerenciador();
        var a = Modelos.Camera(IdA.ToString("D"), senha: Senha) with { ParteImagem = 1 };
        var b = Modelos.Camera(IdB.ToString("D"), senha: Senha) with { ParteImagem = 2 };

        await ger.ReconciliarAsync(new[] { Sessao(IdA), Sessao(IdB) }, Cameras(a, b));
        await Esperar(() => fonte.QuantasAberturas == 2);

        Assert.Equal(new[] { 1, 2 }, fonte.Aberturas.Select(x => x.Camera.ParteImagem).OrderBy(x => x).ToArray());
        Assert.Equal(2, fonte.Aberturas.Select(x => x.Camera.CameraId).Distinct().Count());
        await ger.ReconciliarAsync(Array.Empty<SessaoAoVivoServidor>(), Cameras(a, b));
    }
}

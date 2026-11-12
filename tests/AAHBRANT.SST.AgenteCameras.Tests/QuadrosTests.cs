using System.Drawing;
using AAHBRANT.SST.AgenteCameras.Opcoes;
using AAHBRANT.SST.AgenteCameras.Quadros;
using AAHBRANT.SST.AgenteCameras.Seguranca;
using Microsoft.Extensions.Logging.Abstractions;

namespace AAHBRANT.SST.AgenteCameras.Tests;

public class SeparadorJpegTests
{
    private static byte[] Quadro(int tamanhoMiolo, byte preenchimento)
    {
        var miolo = Enumerable.Repeat(preenchimento, tamanhoMiolo).ToArray();
        return new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }.Concat(miolo).Concat(new byte[] { 0xFF, 0x00, 0x11 }).Concat(new byte[] { 0xFF, 0xD9 }).ToArray();
    }

    [Fact]
    public void DoisQuadrosNoMesmoBloco()
    {
        var a = Quadro(100, 1);
        var b = Quadro(50, 2);
        var saida = new SeparadorJpeg().Adicionar(a.Concat(b).ToArray());
        Assert.Equal(2, saida.Count);
        Assert.Equal(a, saida[0]);
        Assert.Equal(b, saida[1]);
    }

    [Fact]
    public void QuadroCortadoEntreLeiturasSaiCompletoNaSeguinte()
    {
        var a = Quadro(1000, 7);
        var sep = new SeparadorJpeg();
        Assert.Empty(sep.Adicionar(a.AsSpan(0, 400)));
        Assert.Empty(sep.Adicionar(a.AsSpan(400, 300)));
        var saida = sep.Adicionar(a.AsSpan(700));
        Assert.Single(saida);
        Assert.Equal(a, saida[0]);
    }

    [Fact]
    public void MarcadorFinalPartidoEntreDoisBlocos()
    {
        var a = Quadro(10, 3);
        var sep = new SeparadorJpeg();
        Assert.Empty(sep.Adicionar(a.AsSpan(0, a.Length - 1))); // falta só o D9
        var saida = sep.Adicionar(a.AsSpan(a.Length - 1));
        Assert.Single(saida);
        Assert.Equal(a, saida[0]);
    }

    [Fact]
    public void MarcadorInicialPartidoEntreDoisBlocos()
    {
        var a = Quadro(10, 3);
        var sep = new SeparadorJpeg();
        Assert.Empty(sep.Adicionar(new byte[] { 0x00, 0x01, 0xFF })); // FF sozinho no fim
        var saida = sep.Adicionar(a.AsSpan(1)); // começa em D8
        Assert.Single(saida);
        Assert.Equal(a, saida[0]);
    }

    [Fact]
    public void LixoAntesDoPrimeiroQuadroEhDescartado()
    {
        var a = Quadro(20, 9);
        var sep = new SeparadorJpeg();
        var saida = sep.Adicionar(new byte[] { 1, 2, 3, 4 }.Concat(a).ToArray());
        Assert.Single(saida);
        Assert.Equal(a, saida[0]);
        Assert.Equal(4, sep.BytesDescartados);
    }

    [Fact]
    public void ByteAAByteProduzOsMesmosQuadros()
    {
        var a = Quadro(300, 5);
        var b = Quadro(200, 6);
        var sep = new SeparadorJpeg();
        var todos = new List<byte[]>();
        foreach (var x in a.Concat(b)) todos.AddRange(sep.Adicionar(new[] { x }));
        Assert.Equal(2, todos.Count);
        Assert.Equal(a, todos[0]);
        Assert.Equal(b, todos[1]);
    }

    [Fact]
    public void QuadroQueNaoTerminaNaoCresceSemLimite()
    {
        var sep = new SeparadorJpeg();
        var bloco = new byte[1024 * 1024];
        bloco[0] = 0xFF; bloco[1] = 0xD8;
        for (var i = 0; i < 20; i++) sep.Adicionar(bloco.AsSpan(i == 0 ? 0 : 2));
        Assert.True(sep.BytesDescartados > 0);
        // e depois volta ao normal
        var a = Quadro(10, 1);
        Assert.Single(sep.Adicionar(a));
    }
}

public class DetectorMovimentoTests
{
    private const int N = DetectorMovimento.LarguraAmostra * DetectorMovimento.AlturaAmostra;

    private static byte[] Uniforme(byte v) => Enumerable.Repeat(v, N).ToArray();

    [Fact]
    public void PrimeiroQuadroSemReferenciaContaComoMovimento() =>
        Assert.True(DetectorMovimento.HouveMovimento(null, Uniforme(100)));

    [Fact]
    public void CenaIgualNaoTemMovimento() =>
        Assert.False(DetectorMovimento.HouveMovimento(Uniforme(100), Uniforme(100)));

    [Fact]
    public void MudancaGlobalDeBrilhoNaoContaComoMovimento() =>
        Assert.False(DetectorMovimento.HouveMovimento(Uniforme(60), Uniforme(160)));

    [Fact]
    public void LimiarDeDoisPorCentoDosPixels()
    {
        var anterior = Uniforme(100);
        var atual = Uniforme(100);
        var limite = (int)Math.Ceiling(N * DetectorMovimento.LimiarFracao); // 47 pixels
        for (var i = 0; i < limite; i++) atual[i] = 255; // diferença bem acima de 25
        Assert.True(DetectorMovimento.HouveMovimento(anterior, atual));

        var pouco = Uniforme(100);
        for (var i = 0; i < limite - 3; i++) pouco[i] = 255; // um pouco abaixo de 2%
        Assert.False(DetectorMovimento.HouveMovimento(anterior, pouco));
    }

    [Fact]
    public void DiferencaAbaixoDoLimiarPorPixelNaoConta()
    {
        var anterior = Uniforme(100);
        var atual = Uniforme(100);
        for (var i = 0; i < N; i += 2) atual[i] = 120; // +20 (< 25), mesmo em metade dos pixels
        Assert.Equal(0, DetectorMovimento.FracaoAlterada(anterior, atual));
    }

    [Fact]
    public void TamanhosDiferentesOuVaziosNaoGeramMovimento()
    {
        Assert.Equal(0, DetectorMovimento.FracaoAlterada(new byte[10], new byte[20]));
        Assert.Equal(0, DetectorMovimento.FracaoAlterada(Array.Empty<byte>(), Array.Empty<byte>()));
    }

    [Fact]
    public void AmostradorReduzParaGradeFixaECinza()
    {
        var jpeg = Modelos.JpegSintetico(Color.White, 640, 360);
        var amostra = new AmostradorGdi().Amostrar(jpeg);
        Assert.NotNull(amostra);
        Assert.Equal(N, amostra!.Length);
        Assert.All(amostra, v => Assert.InRange(v, 250, 255));
        var preta = new AmostradorGdi().Amostrar(Modelos.JpegSintetico(Color.Black, 640, 360))!;
        Assert.All(preta, v => Assert.InRange(v, 0, 5));
    }

    [Fact]
    public void AmostradorDevolveNuloParaJpegIlegivel() =>
        Assert.Null(new AmostradorGdi().Amostrar(new byte[] { 1, 2, 3 }));

    [Fact]
    public void ObjetoQueEntraNaCenaGeraMovimentoNoJpegReal()
    {
        byte[] Cena(bool comObjeto)
        {
            using var bmp = new Bitmap(640, 360);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(90, 90, 90));
                if (comObjeto) g.FillRectangle(Brushes.White, 200, 100, 240, 180);
            }
            using var ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
            return ms.ToArray();
        }
        var amostrador = new AmostradorGdi();
        var vazia = amostrador.Amostrar(Cena(false))!;
        Assert.False(DetectorMovimento.HouveMovimento(vazia, amostrador.Amostrar(Cena(false))!));
        Assert.True(DetectorMovimento.HouveMovimento(vazia, amostrador.Amostrar(Cena(true))!));
    }

    [Fact]
    public void LimitadorReduzJpegQueExcedeOLimite()
    {
        using var bmp = new Bitmap(1600, 1200);
        var rnd = new Random(1);
        for (var y = 0; y < bmp.Height; y += 1)
            for (var x = 0; x < bmp.Width; x += 1)
                bmp.SetPixel(x, y, Color.FromArgb(rnd.Next(256), rnd.Next(256), rnd.Next(256)));
        using var ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
        var original = ms.ToArray();
        var limite = original.Length / 3;

        var reduzido = LimitadorTamanho.Garantir(original, limite);
        Assert.True(reduzido.Length <= limite, $"{reduzido.Length} > {limite}");
        Assert.NotNull(new AmostradorGdi().Amostrar(reduzido));
        Assert.Same(original, LimitadorTamanho.Garantir(original, original.Length + 1));
    }
}

public class PoliticaEnvioTests
{
    private static byte[] Cena(byte v) => Enumerable.Repeat(v, DetectorMovimento.LarguraAmostra * DetectorMovimento.AlturaAmostra).ToArray();
    private static byte[] CenaComObjeto() { var c = Cena(100); for (var i = 0; i < 200; i++) c[i] = 255; return c; }
    private static readonly DateTimeOffset T0 = new(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DoisPrimeirosQuadrosDescartadosOTerceiroViraReferencia()
    {
        var p = new PoliticaEnvio(TimeSpan.FromSeconds(3));
        Assert.False(p.DeveEnviar(CenaComObjeto(), T0));
        Assert.False(p.DeveEnviar(Cena(100), T0.AddSeconds(1)));
        Assert.False(p.DeveEnviar(CenaComObjeto(), T0.AddSeconds(2))); // referência, mesmo sendo diferente
        Assert.True(p.DeveEnviar(Cena(100), T0.AddSeconds(3)));        // agora compara com a referência
    }

    [Fact]
    public void SoEnviaComMovimento()
    {
        var p = new PoliticaEnvio(TimeSpan.FromSeconds(3));
        for (var i = 0; i < 3; i++) p.DeveEnviar(Cena(100), T0.AddSeconds(i));
        Assert.False(p.DeveEnviar(Cena(100), T0.AddSeconds(10)));
        Assert.False(p.DeveEnviar(Cena(100), T0.AddSeconds(11)));
    }

    [Fact]
    public void RespeitaIntervaloMinimoEntreEnvios()
    {
        var p = new PoliticaEnvio(TimeSpan.FromSeconds(3));
        for (var i = 0; i < 3; i++) p.DeveEnviar(Cena(100), T0.AddSeconds(i));
        Assert.True(p.DeveEnviar(CenaComObjeto(), T0.AddSeconds(10)));   // movimento
        Assert.False(p.DeveEnviar(Cena(100), T0.AddSeconds(11)));        // movimento, mas só 1 s depois
        Assert.False(p.DeveEnviar(CenaComObjeto(), T0.AddSeconds(12)));  // movimento, 2 s depois
        Assert.True(p.DeveEnviar(Cena(100), T0.AddSeconds(13)));         // 3 s: libera
    }

    [Fact]
    public void ReiniciarRecomecaODescarteInicial()
    {
        var p = new PoliticaEnvio(TimeSpan.FromSeconds(1));
        for (var i = 0; i < 3; i++) p.DeveEnviar(Cena(100), T0.AddSeconds(i));
        p.Reiniciar();
        Assert.False(p.DeveEnviar(CenaComObjeto(), T0.AddSeconds(10)));
        Assert.False(p.DeveEnviar(Cena(100), T0.AddSeconds(11)));
        Assert.False(p.DeveEnviar(CenaComObjeto(), T0.AddSeconds(12)));
    }
}

public class UrlRtspTests
{
    [Fact]
    public void UrlPadraoDaCameraDeTeste()
    {
        var c = new CameraConfig("c", "n", "192.168.0.8", 554, "admin", "abc123", 1, 1, null, TimeSpan.FromSeconds(3));
        Assert.Equal("rtsp://192.168.0.8:554/user=admin&password=abc123&channel=1&stream=1.sdp?real_stream", UrlRtsp.Montar(c));
    }

    [Fact]
    public void ModeloPersonalizadoEEscapeDeCaracteresEspeciais()
    {
        var c = new CameraConfig("c", "n", "10.0.0.5", 8554, "op", "p&s@s", 2, 0, "rtsp://{usuario}:{senha}@{host}:{porta}/ch{canal}/{stream}", TimeSpan.FromSeconds(3));
        Assert.Equal("rtsp://op:p%26s%40s@10.0.0.5:8554/ch2/0", UrlRtsp.Montar(c));
    }

    [Fact]
    public void MarcadoresDesconhecidosSaoDetectados() =>
        Assert.Equal(new[] { "{foo}" }, UrlRtsp.MarcadoresInvalidos("rtsp://{host}/{foo}"));

    [Fact]
    public void ArgumentosDoFfmpegTemTransporteTcpTimeoutEFiltro()
    {
        var args = FfmpegFonteQuadros.MontarArgumentos("rtsp://x", 10);
        Assert.Contains("-rtsp_transport", args);
        Assert.Equal("tcp", args[args.ToList().IndexOf("-rtsp_transport") + 1]);
        Assert.Equal("10000000", args[args.ToList().IndexOf("-timeout") + 1]);
        Assert.DoesNotContain("-rw_timeout", args); // não existe para RTSP: o ffmpeg recusa abrir a câmera
        Assert.Contains("fps=1,scale='min(1280,iw)':-2", args);
        Assert.Equal("pipe:1", args[^1]);
    }

    [Fact]
    public async Task FfmpegAusenteGeraErroClaroSemIniciarProcesso()
    {
        var fonte = new FfmpegFonteQuadros(@"C:\nao\existe\ffmpeg.exe", new Mascarador(), NullLogger<FfmpegFonteQuadros>.Instance);
        var ex = await Assert.ThrowsAsync<FfmpegIndisponivelException>(async () =>
        {
            await foreach (var _ in fonte.LerQuadrosAsync(Modelos.Camera(), CancellationToken.None)) { }
        });
        Assert.Contains("ffmpeg não encontrado", ex.Message);
    }
}

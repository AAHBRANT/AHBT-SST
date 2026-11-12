using System.Net;
using System.Text;
using AAHBRANT.SST.AgenteCameras.Envio;
using AAHBRANT.SST.AgenteCameras.Fila;
using AAHBRANT.SST.AgenteCameras.Seguranca;

namespace AAHBRANT.SST.AgenteCameras.Tests;

public class ClienteQuadrosTests
{
    private sealed class Handler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Resposta { get; set; } =
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        public HttpRequestMessage? Ultima { get; private set; }
        public string? CorpoTexto { get; private set; }
        public byte[]? CorpoBytes { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Ultima = request;
            CorpoBytes = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(ct);
            CorpoTexto = CorpoBytes is null ? null : Encoding.Latin1.GetString(CorpoBytes);
            return await Resposta(request, ct);
        }
    }

    private static (ClienteQuadros Cliente, Handler Handler) Criar(TimeSpan? limite = null)
    {
        var h = new Handler();
        var http = new HttpClient(h) { Timeout = Timeout.InfiniteTimeSpan };
        return (new ClienteQuadros(http, new Uri("https://api.exemplo.com.br/"), "agente-9", "SEGREDO-123", new Mascarador(), limite), h);
    }

    private static HttpResponseMessage Json(HttpStatusCode s, string json) =>
        new(s) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static readonly byte[] Jpeg = { 0xFF, 0xD8, 1, 2, 3, 0xFF, 0xD9 };

    [Fact]
    public async Task MontaARequisicaoDoContrato()
    {
        var (cliente, h) = Criar();
        var em = new DateTimeOffset(2026, 11, 12, 9, 30, 15, TimeSpan.FromHours(-3));
        await cliente.EnviarAsync("cam 1", Jpeg, em, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, h.Ultima!.Method);
        Assert.Equal("https://api.exemplo.com.br/api/agentes-camera/agente-9/cameras/cam%201/quadros", h.Ultima.RequestUri!.AbsoluteUri);
        Assert.Equal("SEGREDO-123", h.Ultima.Headers.GetValues("X-Agente-Segredo").Single());
        Assert.Matches(@"^\d+\.\d+\.\d+$", h.Ultima.Headers.GetValues("X-Agente-Versao").Single());
        Assert.StartsWith("multipart/form-data", h.Ultima.Content!.Headers.ContentType!.MediaType == "multipart/form-data" ? "multipart/form-data" : "");
        var corpo = h.CorpoTexto!;
        Assert.Contains("name=Foto; filename=quadro.jpg", corpo);
        Assert.Contains("Content-Type: image/jpeg", corpo);
        Assert.Contains("name=CapturadaEm", corpo);
        Assert.Contains("2026-11-12T09:30:15.0000000-03:00", corpo);
        Assert.Contains(Encoding.Latin1.GetString(Jpeg), corpo);
        Assert.DoesNotContain("SEGREDO-123", corpo);
    }

    [Fact]
    public async Task Status200LeOResumo()
    {
        var (cliente, h) = Criar();
        var id = Guid.NewGuid();
        h.Resposta = (_, _) => Task.FromResult(Json(HttpStatusCode.OK,
            $$"""{"situacao":"ocorrencia","ocorrenciaId":"{{id}}","alertaEnviado":true,"pessoas":3,"semCapacete":1,"semColete":2,"trabalhadorId":null,"trabalhadorNome":null,"repetida":false}"""));
        var r = await cliente.EnviarAsync("c", Jpeg, DateTimeOffset.Now, CancellationToken.None);
        Assert.Equal(TipoResultado.Sucesso, r.Tipo);
        Assert.Equal("ocorrencia", r.Resposta!.Situacao);
        Assert.Equal(id, r.Resposta.OcorrenciaId);
        Assert.True(r.Resposta.AlertaEnviado);
        Assert.Equal(3, r.Resposta.Pessoas);
        Assert.Equal(2, r.Resposta.SemColete);
    }

    [Fact]
    public async Task Status200SemJsonLegivelContinuaSendoSucesso()
    {
        var (cliente, h) = Criar();
        h.Resposta = (_, _) => Task.FromResult(Json(HttpStatusCode.OK, "<html>"));
        var r = await cliente.EnviarAsync("c", Jpeg, DateTimeOffset.Now, CancellationToken.None);
        Assert.Equal(TipoResultado.Sucesso, r.Tipo);
        Assert.Null(r.Resposta);
    }

    [Theory]
    [InlineData(401, TipoResultado.SegredoInvalido)]
    [InlineData(404, TipoResultado.SemInternet)] // 404 sem o codigo camera_nao_encontrada = indisponibilidade
    [InlineData(409, TipoResultado.SemInternet)]
    [InlineData(400, TipoResultado.Descartado)]
    [InlineData(413, TipoResultado.Descartado)]
    [InlineData(500, TipoResultado.SemInternet)]
    [InlineData(502, TipoResultado.SemInternet)]
    [InlineData(503, TipoResultado.SemInternet)]
    [InlineData(504, TipoResultado.SemInternet)]
    public async Task MapeiaCadaStatus(int status, TipoResultado esperado)
    {
        var (cliente, h) = Criar();
        h.Resposta = (_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status));
        var r = await cliente.EnviarAsync("c", Jpeg, DateTimeOffset.Now, CancellationToken.None);
        Assert.Equal(esperado, r.Tipo);
        Assert.Equal(status, r.StatusHttp);
    }

    [Fact]
    public async Task Status429LeRetryAfterEmSegundos()
    {
        var (cliente, h) = Criar();
        h.Resposta = (_, _) =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            resp.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(17));
            return Task.FromResult(resp);
        };
        var r = await cliente.EnviarAsync("c", Jpeg, DateTimeOffset.Now, CancellationToken.None);
        Assert.Equal(TipoResultado.LimiteTaxa, r.Tipo);
        Assert.Equal(TimeSpan.FromSeconds(17), r.EsperarPor);
    }

    [Fact]
    public async Task Status429SemRetryAfterNaoInventaEspera()
    {
        var (cliente, h) = Criar();
        h.Resposta = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        var r = await cliente.EnviarAsync("c", Jpeg, DateTimeOffset.Now, CancellationToken.None);
        Assert.Equal(TipoResultado.LimiteTaxa, r.Tipo);
        Assert.Null(r.EsperarPor);
    }

    [Fact]
    public async Task ErroDeRedeViraSemInternet()
    {
        var (cliente, h) = Criar();
        h.Resposta = (_, _) => throw new HttpRequestException("Nenhum host conhecido");
        var r = await cliente.EnviarAsync("c", Jpeg, DateTimeOffset.Now, CancellationToken.None);
        Assert.Equal(TipoResultado.SemInternet, r.Tipo);
        Assert.Null(r.StatusHttp);
    }

    [Fact]
    public async Task TimeoutViraSemInternet()
    {
        var (cliente, h) = Criar(TimeSpan.FromMilliseconds(100));
        h.Resposta = async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); return new HttpResponseMessage(); };
        var r = await cliente.EnviarAsync("c", Jpeg, DateTimeOffset.Now, CancellationToken.None);
        Assert.Equal(TipoResultado.SemInternet, r.Tipo);
        Assert.Contains("tempo esgotado", r.Detalhe);
    }

    [Fact]
    public async Task CancelamentoDoServicoPropagaEmVezDeVirarSemInternet()
    {
        var (cliente, h) = Criar();
        h.Resposta = async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); return new HttpResponseMessage(); };
        using var cts = new CancellationTokenSource(50);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cliente.EnviarAsync("c", Jpeg, DateTimeOffset.Now, cts.Token));
    }

    [Fact]
    public void TempoLimitePadraoEDe90Segundos() => Assert.Equal(TimeSpan.FromSeconds(90), ClienteQuadros.TempoLimite);
}

// Cliente cujo resultado depende do quadro enviado.
public sealed class ClienteSeletivo : IClienteQuadros
{
    private readonly Func<(byte[] Jpeg, DateTimeOffset Em), ResultadoEnvio> _regra;
    public List<(byte[] Jpeg, DateTimeOffset Em)> Chamadas { get; } = new();
    public ClienteSeletivo(Func<(byte[] Jpeg, DateTimeOffset Em), ResultadoEnvio> regra) => _regra = regra;

    public Task<ResultadoEnvio> EnviarAsync(string cameraId, byte[] jpeg, DateTimeOffset capturadaEm, CancellationToken ct)
    {
        lock (Chamadas) Chamadas.Add((jpeg, capturadaEm));
        return Task.FromResult(_regra((jpeg, capturadaEm)));
    }
}

public class EnviadorCameraTests : IDisposable
{
    private readonly PastaTemporaria _pasta = new();
    private readonly RelogioFalso _relogio = new();
    private readonly ClienteFalso _cliente = new();
    private readonly LogColetor _log = new();
    private readonly EstadoApi _estado = new();
    private readonly FilaQuadros _fila;
    private readonly EnviadorCamera _enviador;

    public EnviadorCameraTests()
    {
        _fila = new FilaQuadros(_pasta.Caminho, new ProtetorFalso(), _relogio, new LogColetor<FilaQuadros>(), TimeSpan.FromHours(24), 500L << 20);
        _enviador = new EnviadorCamera(Modelos.Camera(), _cliente, _fila, _estado, _relogio, _log);
    }

    public void Dispose() => _pasta.Dispose();

    private static byte[] Jpeg(byte m) => new byte[] { 0xFF, 0xD8, m, 0xFF, 0xD9 };

    [Fact]
    public async Task EnvioAoVivoComSucessoLogaUmaLinhaComResumoESemDados()
    {
        _enviador.OferecerVivo(Jpeg(1), _relogio.Agora);
        Assert.Null(await _enviador.PassoAsync(CancellationToken.None));

        Assert.Single(_cliente.Chamadas);
        Assert.False(_enviador.Offline);
        var linha = _log.Linhas.Single(l => l.Nivel == Microsoft.Extensions.Logging.LogLevel.Information).Texto;
        Assert.Contains("Portaria", linha);
        Assert.Contains("HTTP 200", linha);
        Assert.Contains("situacao=ocorrencia", linha);
        Assert.Contains("semCapacete=1", linha);
        Assert.DoesNotContain("s3nh@Secreta", _log.Tudo());
    }

    [Fact]
    public async Task SemInternetGuardaEmDiscoEEnviaDepoisComOHorarioOriginal()
    {
        var capturada = _relogio.Agora.AddSeconds(-7);
        _cliente.Enfileirar(ClienteFalso.Rede());
        _enviador.OferecerVivo(Jpeg(1), capturada);
        await _enviador.PassoAsync(CancellationToken.None);

        Assert.True(_enviador.Offline);
        Assert.Equal(1, _fila.Contar("cam-1"));

        // Dentro da janela de espera não tenta de novo.
        var espera = await _enviador.PassoAsync(CancellationToken.None);
        Assert.NotNull(espera);
        Assert.Single(_cliente.Chamadas);

        // Passada a espera, a API voltou: drena com o horário real da captura.
        _relogio.Avancar(TimeSpan.FromMinutes(2));
        Assert.Null(await _enviador.PassoAsync(CancellationToken.None));
        Assert.Equal(2, _cliente.Chamadas.Count);
        Assert.Equal(capturada, _cliente.Chamadas[1].Em);
        Assert.Equal(0, _fila.Contar("cam-1"));
        Assert.False(_enviador.Offline);
    }

    [Fact]
    public async Task QuadroEnviadoComFalhaDeRedeNaoSomeDaFilaAteOk()
    {
        _fila.Adicionar("cam-1", Jpeg(1), _relogio.Agora.AddMinutes(-3));
        _cliente.Enfileirar(ClienteFalso.Rede());
        await _enviador.PassoAsync(CancellationToken.None);
        Assert.Equal(1, _fila.Contar("cam-1")); // continua na fila (só apaga após 200/400)
    }

    [Fact]
    public async Task FilaDrenaDoMaisAntigoParaOMaisNovoNoRitmoDoIntervalo()
    {
        var t = _relogio.Agora;
        _fila.Adicionar("cam-1", Jpeg(3), t.AddMinutes(-1));
        _fila.Adicionar("cam-1", Jpeg(1), t.AddMinutes(-30));
        _fila.Adicionar("cam-1", Jpeg(2), t.AddMinutes(-10));

        Assert.Null(await _enviador.PassoAsync(CancellationToken.None));
        // 2º envio logo em seguida: ainda dentro do intervalo de 3 s => espera, sem chamar a API
        var espera = await _enviador.PassoAsync(CancellationToken.None);
        Assert.NotNull(espera);
        Assert.Single(_cliente.Chamadas);

        _relogio.Avancar(TimeSpan.FromSeconds(3));
        Assert.Null(await _enviador.PassoAsync(CancellationToken.None));
        _relogio.Avancar(TimeSpan.FromSeconds(3));
        Assert.Null(await _enviador.PassoAsync(CancellationToken.None));

        Assert.Equal(new byte[] { 1, 2, 3 }, _cliente.Chamadas.Select(c => c.Jpeg[2]).ToArray());
        Assert.Equal(0, _fila.Contar("cam-1"));
    }

    [Fact]
    public async Task QuadroAoVivoTemPrioridadeSobreAFila()
    {
        _fila.Adicionar("cam-1", Jpeg(1), _relogio.Agora.AddMinutes(-30));
        _enviador.OferecerVivo(Jpeg(9), _relogio.Agora);

        await _enviador.PassoAsync(CancellationToken.None);
        Assert.Equal(9, _cliente.Chamadas[0].Jpeg[2]);
        Assert.Equal(1, _fila.Contar("cam-1"));

        // e a fila segue depois, no ritmo
        _relogio.Avancar(TimeSpan.FromSeconds(3));
        await _enviador.PassoAsync(CancellationToken.None);
        Assert.Equal(1, _cliente.Chamadas[1].Jpeg[2]);
    }

    [Fact]
    public async Task EmModoOfflineQuadroNovoVaiDiretoParaODiscoSemChamarAApi()
    {
        _cliente.Enfileirar(ClienteFalso.Rede());
        _enviador.OferecerVivo(Jpeg(1), _relogio.Agora);
        await _enviador.PassoAsync(CancellationToken.None);

        _enviador.OferecerVivo(Jpeg(2), _relogio.Agora.AddSeconds(3));
        _enviador.OferecerVivo(Jpeg(3), _relogio.Agora.AddSeconds(6));
        Assert.Equal(3, _fila.Contar("cam-1"));
        Assert.Single(_cliente.Chamadas);
    }

    [Fact]
    public async Task Status401PausaOsEnviosPor5MinutosELogaErroClaro()
    {
        _fila.Adicionar("cam-1", Jpeg(1), _relogio.Agora.AddMinutes(-1));
        _cliente.Enfileirar(ClienteFalso.Status(TipoResultado.SegredoInvalido, 401));
        await _enviador.PassoAsync(CancellationToken.None);

        Assert.Equal(_relogio.Agora + TimeSpan.FromMinutes(5), _estado.PausadoAte);
        Assert.Contains(_log.Linhas, l => l.Nivel == Microsoft.Extensions.Logging.LogLevel.Error && l.Texto.Contains("401") && l.Texto.Contains("segredo"));

        // durante a pausa: nada é enviado e quadros ao vivo são descartados (sem laço apertado)
        _enviador.OferecerVivo(Jpeg(5), _relogio.Agora);
        var espera = await _enviador.PassoAsync(CancellationToken.None);
        Assert.NotNull(espera);
        Assert.Single(_cliente.Chamadas);
        Assert.Equal(1, _fila.Contar("cam-1")); // fila preservada

        // passados 5 min volta a tentar
        _relogio.Avancar(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        await _enviador.PassoAsync(CancellationToken.None);
        Assert.Equal(2, _cliente.Chamadas.Count);
    }

    [Fact]
    public async Task Status404DeCameraNaoEncontradaPausaPor15MinutosSemApagarAFilaENemEncerrar()
    {
        _fila.Adicionar("cam-1", Jpeg(1), _relogio.Agora.AddMinutes(-1));
        _cliente.Enfileirar(ClienteFalso.Status(TipoResultado.CameraNaoPertence, 404));
        await _enviador.PassoAsync(CancellationToken.None);

        Assert.Equal(_relogio.Agora + TimeSpan.FromMinutes(15), _enviador.CameraPausadaAte);
        Assert.Equal(1, _fila.Contar("cam-1")); // a fila fica; expira sozinha em 24 h
        Assert.Contains(_log.Linhas, l => l.Nivel == Microsoft.Extensions.Logging.LogLevel.Error && l.Texto.Contains("PAUSADA") && l.Texto.Contains("camera_nao_encontrada"));

        // Durante a pausa: nada é enviado e o quadro ao vivo é descartado.
        _enviador.OferecerVivo(Jpeg(2), _relogio.Agora);
        Assert.NotNull(await _enviador.PassoAsync(CancellationToken.None));
        Assert.Single(_cliente.Chamadas);
        Assert.Equal(1, _fila.Contar("cam-1"));

        // Passados 15 min tenta de novo (e, se a câmera foi corrigida no servidor, volta a funcionar).
        _relogio.Avancar(TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(1));
        _enviador.OferecerVivo(Jpeg(3), _relogio.Agora);
        Assert.Null(await _enviador.PassoAsync(CancellationToken.None));
        Assert.Equal(2, _cliente.Chamadas.Count);
        Assert.Equal(3, _cliente.Chamadas[1].Jpeg[2]);
    }

    [Fact]
    public async Task CameraPausadaPor404TentaDeNovoEPausaDeNovoSeContinuarNaoEncontrada()
    {
        _cliente.PadraoQuandoVazio = ClienteFalso.Status(TipoResultado.CameraNaoPertence, 404);
        for (var i = 0; i < 3; i++)
        {
            _enviador.OferecerVivo(Jpeg(1), _relogio.Agora);
            await _enviador.PassoAsync(CancellationToken.None);
            _relogio.Avancar(TimeSpan.FromMinutes(16));
        }
        Assert.Equal(3, _cliente.Chamadas.Count);
        Assert.Equal(3, _log.Linhas.Count(l => l.Texto.Contains("PAUSADA")));
    }

    [Fact]
    public async Task Status404ComumNaoPausaACameraEVaiParaAFilaComRecuo()
    {
        _cliente.Enfileirar(ResultadoEnvio.SemInternet("HTTP 404 sem o código", 404));
        _enviador.OferecerVivo(Jpeg(1), _relogio.Agora);
        await _enviador.PassoAsync(CancellationToken.None);

        Assert.True(_enviador.Offline);
        Assert.Equal(1, _fila.Contar("cam-1"));
        Assert.Equal(DateTimeOffset.MinValue, _enviador.CameraPausadaAte);
    }

    [Fact]
    public async Task SondagemDepoisDaEsperaUsaOQuadroAoVivoEnaoOItemMaisAntigoDaFila()
    {
        _fila.Adicionar("cam-1", Jpeg(1), _relogio.Agora.AddMinutes(-30)); // item antigo
        _cliente.Enfileirar(ClienteFalso.Rede());
        _enviador.OferecerVivo(Jpeg(2), _relogio.Agora);
        await _enviador.PassoAsync(CancellationToken.None);
        Assert.True(_enviador.Offline);

        // Passada a espera, o quadro ao vivo (mesmo com a fila não vazia) é a sondagem.
        _relogio.Avancar(TimeSpan.FromSeconds(6));
        _enviador.OferecerVivo(Jpeg(9), _relogio.Agora);
        await _enviador.PassoAsync(CancellationToken.None);
        Assert.Equal(9, _cliente.Chamadas[1].Jpeg[2]);
        Assert.False(_enviador.Offline);

        // E a fila drena depois, intercalada, na ordem de captura (o 1 era mais antigo que o 2 guardado depois).
        _relogio.Avancar(TimeSpan.FromSeconds(3));
        await _enviador.PassoAsync(CancellationToken.None);
        _relogio.Avancar(TimeSpan.FromSeconds(3));
        await _enviador.PassoAsync(CancellationToken.None);
        Assert.Equal(new byte[] { 2, 9, 1, 2 }, _cliente.Chamadas.Select(c => c.Jpeg[2]).ToArray());
        Assert.Equal(0, _fila.Contar("cam-1"));
    }

    [Fact]
    public async Task OfflineSemQuadroAoVivoOItemDaFilaSondaDepoisDeUmaEsperaCurta()
    {
        _fila.Adicionar("cam-1", Jpeg(1), _relogio.Agora.AddMinutes(-5));
        _cliente.Enfileirar(ClienteFalso.Rede());
        await _enviador.PassoAsync(CancellationToken.None); // o item da fila falha por rede => offline
        Assert.True(_enviador.Offline);

        _relogio.Avancar(TimeSpan.FromSeconds(6)); // espera de rede passou, mas ainda aguarda um quadro ao vivo
        Assert.NotNull(await _enviador.PassoAsync(CancellationToken.None));
        Assert.Single(_cliente.Chamadas);

        _relogio.Avancar(TimeSpan.FromSeconds(20)); // sem vivo: a fila faz a sondagem
        await _enviador.PassoAsync(CancellationToken.None);
        Assert.Equal(2, _cliente.Chamadas.Count);
        Assert.Equal(0, _fila.Contar("cam-1"));
    }

    private static ResultadoEnvio Erro503() => ResultadoEnvio.SemInternet("erro do servidor (HTTP 503)", 503);

    [Fact]
    public async Task ItemDaFilaComErro5xxPersistenteEDescartadoApos5FalhasEnquantoOVivoPassa()
    {
        _fila.Adicionar("cam-1", Jpeg(7), _relogio.Agora.AddMinutes(-10)); // item "envenenado"
        var cliente = new ClienteSeletivo(c => c.Jpeg[2] == 7 ? Erro503() : ClienteFalso.Ok());
        var enviador = new EnviadorCamera(Modelos.Camera(), cliente, _fila, _estado, _relogio, _log);

        var descartado = false;
        for (var i = 0; i < 40 && !descartado; i++)
        {
            enviador.OferecerVivo(Jpeg(1), _relogio.Agora); // ao vivo passa e prova que a API atende
            await enviador.PassoAsync(CancellationToken.None);
            _relogio.Avancar(TimeSpan.FromSeconds(3)); // ritmo da drenagem: um envio por IntervaloEnvio
            await enviador.PassoAsync(CancellationToken.None);
            _relogio.Avancar(TimeSpan.FromSeconds(20));
            descartado = _fila.Contar("cam-1") == 0;
        }

        Assert.True(descartado, "o item envenenado deveria ter sido descartado");
        Assert.False(enviador.Offline);
        Assert.Equal(EnviadorCamera.FalhasParaDescartarItem, cliente.Chamadas.Count(c => c.Jpeg[2] == 7));
        Assert.Contains(_log.Linhas, l => l.Nivel == Microsoft.Extensions.Logging.LogLevel.Warning && l.Texto.Contains("descartado para não travar a fila"));
    }

    [Fact]
    public async Task ItemComErro5xxSemProvaDeQueOVivoPassaNaoEDescartadoEViraModoOffline()
    {
        _fila.Adicionar("cam-1", Jpeg(7), _relogio.Agora.AddMinutes(-10));
        _cliente.PadraoQuandoVazio = Erro503(); // API realmente fora: nenhum ao vivo passou
        for (var i = 0; i < EnviadorCamera.FalhasParaDescartarItem + 2; i++)
        {
            await _enviador.PassoAsync(CancellationToken.None);
            _relogio.Avancar(TimeSpan.FromMinutes(2));
        }
        Assert.Equal(1, _fila.Contar("cam-1")); // continua: não há prova de que o problema é o item
        Assert.True(_enviador.Offline);
    }

    [Fact]
    public async Task ItemDaFilaFalhoNaoImpedeQuadrosAoVivoDeIremDiretoParaAApi()
    {
        _fila.Adicionar("cam-1", Jpeg(7), _relogio.Agora.AddMinutes(-10));
        var cliente = new ClienteSeletivo(c => c.Jpeg[2] == 7 ? Erro503() : ClienteFalso.Ok());
        var enviador = new EnviadorCamera(Modelos.Camera(), cliente, _fila, _estado, _relogio, _log);

        enviador.OferecerVivo(Jpeg(1), _relogio.Agora);
        await enviador.PassoAsync(CancellationToken.None); // vivo OK
        await enviador.PassoAsync(CancellationToken.None); // item falha (API no ar)
        Assert.False(enviador.Offline);

        _relogio.Avancar(TimeSpan.FromSeconds(3));
        enviador.OferecerVivo(Jpeg(2), _relogio.Agora);
        await enviador.PassoAsync(CancellationToken.None);
        Assert.Equal(2, cliente.Chamadas.Last().Jpeg[2]); // o ao vivo foi para a API, não para o disco
        Assert.Equal(1, _fila.Contar("cam-1"));
    }

    [Fact]
    public async Task Status429RespeitaRetryAfterEGuardaOQuadro()
    {
        _cliente.Enfileirar(ClienteFalso.Status(TipoResultado.LimiteTaxa, 429, TimeSpan.FromSeconds(40)));
        _enviador.OferecerVivo(Jpeg(1), _relogio.Agora);
        await _enviador.PassoAsync(CancellationToken.None);
        Assert.Equal(1, _fila.Contar("cam-1"));

        _relogio.Avancar(TimeSpan.FromSeconds(39));
        Assert.NotNull(await _enviador.PassoAsync(CancellationToken.None));
        Assert.Single(_cliente.Chamadas);

        _relogio.Avancar(TimeSpan.FromSeconds(2));
        await _enviador.PassoAsync(CancellationToken.None);
        Assert.Equal(2, _cliente.Chamadas.Count);
        Assert.Equal(0, _fila.Contar("cam-1"));
    }

    [Fact]
    public async Task Status429SemRetryAfterRecuaEComEsperaCrescente()
    {
        _cliente.Enfileirar(ClienteFalso.Status(TipoResultado.LimiteTaxa, 429), ClienteFalso.Status(TipoResultado.LimiteTaxa, 429));
        _enviador.OferecerVivo(Jpeg(1), _relogio.Agora);
        await _enviador.PassoAsync(CancellationToken.None);
        var primeira = await _enviador.PassoAsync(CancellationToken.None);
        Assert.InRange(primeira!.Value.TotalSeconds, 25, 31);

        _relogio.Avancar(TimeSpan.FromSeconds(31));
        await _enviador.PassoAsync(CancellationToken.None); // drena o guardado, leva 429 de novo
        var segunda = await _enviador.PassoAsync(CancellationToken.None);
        Assert.True(segunda > primeira);
    }

    [Fact]
    public async Task Status400DescartaOQuadroAoVivoEOQuadroDaFila()
    {
        _cliente.Enfileirar(ClienteFalso.Status(TipoResultado.Descartado, 400));
        _enviador.OferecerVivo(Jpeg(1), _relogio.Agora);
        await _enviador.PassoAsync(CancellationToken.None);
        Assert.Equal(0, _fila.Contar("cam-1"));
        Assert.False(_enviador.Offline);

        _fila.Adicionar("cam-1", Jpeg(2), _relogio.Agora.AddMinutes(-1));
        _cliente.Enfileirar(ClienteFalso.Status(TipoResultado.Descartado, 400));
        _relogio.Avancar(TimeSpan.FromSeconds(5));
        await _enviador.PassoAsync(CancellationToken.None);
        Assert.Equal(0, _fila.Contar("cam-1")); // 400 não adianta repetir
    }

    [Fact]
    public async Task Status5xxTrataComoSemInternet()
    {
        _cliente.Enfileirar(ResultadoEnvio.SemInternet("erro do servidor (HTTP 503)", 503));
        _enviador.OferecerVivo(Jpeg(1), _relogio.Agora);
        await _enviador.PassoAsync(CancellationToken.None);
        Assert.True(_enviador.Offline);
        Assert.Equal(1, _fila.Contar("cam-1"));
    }

    [Fact]
    public async Task NuncaHaDoisEnviosSimultaneosDaMesmaCameraEOQuadroMaisRecenteSubstituiOPendente()
    {
        var liberar = new TaskCompletionSource();
        var primeiroIniciou = new TaskCompletionSource();
        var chamadas = 0;
        _cliente.Bloqueio = async () =>
        {
            if (Interlocked.Increment(ref chamadas) == 1)
            {
                primeiroIniciou.SetResult();
                await liberar.Task;
            }
        };

        using var cts = new CancellationTokenSource();
        var trabalhador = Task.Run(() => _enviador.ExecutarAsync(cts.Token));
        _enviador.OferecerVivo(Jpeg(1), _relogio.Agora);
        await primeiroIniciou.Task.WaitAsync(TimeSpan.FromSeconds(5));

        _enviador.OferecerVivo(Jpeg(2), _relogio.Agora);
        _enviador.OferecerVivo(Jpeg(3), _relogio.Agora);
        await Task.Delay(100);
        Assert.Single(_cliente.Chamadas); // o segundo espera a resposta do primeiro

        liberar.SetResult();
        var limite = DateTime.UtcNow.AddSeconds(5);
        while (_cliente.Chamadas.Count < 2 && DateTime.UtcNow < limite) await Task.Delay(20);
        cts.Cancel();
        await trabalhador.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, _cliente.MaximoSimultaneo);
        Assert.Equal(new byte[] { 1, 3 }, _cliente.Chamadas.Select(c => c.Jpeg[2]).ToArray()); // o 2 foi substituído pelo 3
    }

    [Fact]
    public async Task QuadroQueChegaDuranteEnvioQueFalhaPorRedeTambemVaiParaODisco()
    {
        var liberar = new TaskCompletionSource();
        var iniciou = new TaskCompletionSource();
        _cliente.Bloqueio = async () => { iniciou.TrySetResult(); await liberar.Task; };
        _cliente.Enfileirar(ClienteFalso.Rede());

        _enviador.OferecerVivo(Jpeg(1), _relogio.Agora);
        var passo = Task.Run(() => _enviador.PassoAsync(CancellationToken.None));
        await iniciou.Task.WaitAsync(TimeSpan.FromSeconds(5));
        _enviador.OferecerVivo(Jpeg(2), _relogio.Agora.AddSeconds(3));
        liberar.SetResult();
        await passo;

        Assert.Equal(2, _fila.Contar("cam-1"));
    }
}

using System.Net;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using AAHBRANT.SST.AgenteCameras.Envio;
using AAHBRANT.SST.AgenteCameras.Fila;
using AAHBRANT.SST.AgenteCameras.Infra;
using AAHBRANT.SST.AgenteCameras.Opcoes;
using AAHBRANT.SST.AgenteCameras.Seguranca;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.AgenteCameras.Tests;

// Correções da revisão: 404 só é "câmera não pertence" com o código no corpo, URL base com caminho, GUIDs na
// configuração, limpeza periódica da fila em disco, supervisor das tarefas e permissão da pasta de dados.

public class ClienteQuadros404Tests
{
    private sealed class Handler : HttpMessageHandler
    {
        public Func<HttpResponseMessage> Resposta { get; set; } = () => new HttpResponseMessage(HttpStatusCode.OK);
        public Uri? Url { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Url = request.RequestUri;
            return Task.FromResult(Resposta());
        }
    }

    private static readonly byte[] Jpeg = { 0xFF, 0xD8, 1, 0xFF, 0xD9 };

    private static (ClienteQuadros Cliente, Handler Handler) Criar(string baseUrl = "https://api.exemplo.com.br/")
    {
        var h = new Handler();
        return (new ClienteQuadros(new HttpClient(h), new Uri(baseUrl), "agente-9", "SEGREDO", new Mascarador()), h);
    }

    private static HttpResponseMessage Json(HttpStatusCode s, string json) =>
        new(s) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Theory]
    [InlineData("""{"codigo":"camera_nao_encontrada"}""")]
    [InlineData("""{"codigo":"camera_nao_encontrada","mensagem":"Câmera não pertence a este agente."}""")]
    [InlineData("""{"Codigo":"CAMERA_NAO_ENCONTRADA"}""")]
    public async Task Status404ComOCodigoNoCorpoEACameraNaoPertence(string corpo)
    {
        var (cliente, h) = Criar();
        h.Resposta = () => Json(HttpStatusCode.NotFound, corpo);
        var r = await cliente.EnviarAsync("c", Jpeg, DateTimeOffset.Now, CancellationToken.None);
        Assert.Equal(TipoResultado.CameraNaoPertence, r.Tipo);
        Assert.Equal(404, r.StatusHttp);
    }

    [Theory]
    [InlineData("")]
    [InlineData("<html>404 Not Found</html>")]
    [InlineData("""{"codigo":"outra_coisa"}""")]
    [InlineData("""{"type":"about:blank","title":"Not Found","status":404}""")]
    [InlineData("""["camera_nao_encontrada"]""")]
    [InlineData("""{"codigo":123}""")]
    public async Task Status404SemOCodigoETratadoComoIndisponibilidade(string corpo)
    {
        var (cliente, h) = Criar();
        h.Resposta = () => Json(HttpStatusCode.NotFound, corpo);
        var r = await cliente.EnviarAsync("c", Jpeg, DateTimeOffset.Now, CancellationToken.None);
        Assert.Equal(TipoResultado.SemInternet, r.Tipo);
        Assert.Equal(404, r.StatusHttp);
        Assert.Contains("camera_nao_encontrada", r.Detalhe); // a mensagem diz qual código faltou
    }

    [Theory]
    [InlineData(409, TipoResultado.SemInternet)]
    [InlineData(503, TipoResultado.SemInternet)]
    [InlineData(400, TipoResultado.Descartado)]
    [InlineData(413, TipoResultado.Descartado)]
    public async Task Status409E503TentamDeNovoEStatus400E413Descartam(int status, TipoResultado esperado)
    {
        var (cliente, h) = Criar();
        h.Resposta = () => new HttpResponseMessage((HttpStatusCode)status);
        var r = await cliente.EnviarAsync("c", Jpeg, DateTimeOffset.Now, CancellationToken.None);
        Assert.Equal(esperado, r.Tipo);
        Assert.Equal(status, r.StatusHttp);
    }

    [Theory]
    [InlineData("https://host/sst", "https://host/sst/api/agentes-camera/agente-9/cameras/c/quadros")]
    [InlineData("https://host/sst/", "https://host/sst/api/agentes-camera/agente-9/cameras/c/quadros")]
    [InlineData("https://host/a/b", "https://host/a/b/api/agentes-camera/agente-9/cameras/c/quadros")]
    [InlineData("https://host", "https://host/api/agentes-camera/agente-9/cameras/c/quadros")]
    [InlineData("https://host/", "https://host/api/agentes-camera/agente-9/cameras/c/quadros")]
    [InlineData("http://localhost:5000", "http://localhost:5000/api/agentes-camera/agente-9/cameras/c/quadros")]
    public async Task ApiBaseUrlComCaminhoPreservaOCaminhoBaseComOuSemBarraFinal(string baseUrl, string esperada)
    {
        var (cliente, h) = Criar(baseUrl);
        await cliente.EnviarAsync("c", Jpeg, DateTimeOffset.Now, CancellationToken.None);
        Assert.Equal(esperada, h.Url!.AbsoluteUri);
    }
}

public class ValidadorGuidTests
{
    private const string AgenteGuid = "3f2504e0-4f89-11d3-9a0c-0305e82c3301";
    private const string CameraGuid = "11111111-1111-1111-1111-111111111111";

    private static AgenteCamerasOptions Base() => new()
    {
        ApiBaseUrl = "https://api.exemplo.com.br",
        AgenteId = AgenteGuid,
        Segredo = "segredo",
        CaminhoFfmpeg = @"C:\ffmpeg\ffmpeg.exe",
        Cameras = { new CameraOptions { CameraId = CameraGuid, Nome = "Portaria", Host = "192.168.0.8", Senha = "abc" } },
    };

    private static ResultadoConfiguracao Validar(AgenteCamerasOptions o) => ValidadorConfiguracao.Validar(o, new ProtetorFalso());

    [Theory]
    [InlineData("agente-1")]
    [InlineData("12345")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("3f2504e0-4f89-11d3-9a0c-0305e82c33")]
    public void AgenteIdQueNaoEGuidImpedeAPartidaComMensagemClara(string valor)
    {
        var o = Base();
        o.AgenteId = valor;
        var r = Validar(o);
        Assert.False(r.Valida);
        var erro = Assert.Single(r.Erros, e => e.Contains("AgenteId"));
        Assert.Contains("GUID", erro);
        Assert.Contains(valor, erro);
    }

    [Fact]
    public void AgenteIdEmOutroFormatoDeGuidEAceitoENormalizadoParaAUrl()
    {
        var o = Base();
        o.AgenteId = "{3F2504E0-4F89-11D3-9A0C-0305E82C3301}";
        var r = Validar(o);
        Assert.True(r.Valida, string.Join("; ", r.Erros));
        Assert.Equal(AgenteGuid, r.Configuracao!.AgenteId);
    }

    [Theory]
    [InlineData("c1")]
    [InlineData("camera-portaria")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void CameraIdQueNaoEGuidTiraSoEssaCameraDoArComAvisoClaro(string valor)
    {
        var o = Base();
        o.Cameras.Add(new CameraOptions { CameraId = valor, Nome = "Ruim", Host = "h", Senha = "x" });
        var r = Validar(o);
        Assert.True(r.Valida);
        Assert.Single(r.Configuracao!.Cameras);
        var aviso = Assert.Single(r.Avisos, a => a.Contains("Ruim"));
        Assert.Contains("NÃO ativada", aviso);
        Assert.Contains("GUID", aviso);
    }

    [Fact]
    public void CameraIdRepetidoEmMaiusculasEmOutroFormatoContaComoRepetido()
    {
        var o = Base();
        o.Cameras.Add(new CameraOptions { CameraId = CameraGuid.ToUpperInvariant().Replace("-", ""), Nome = "Repetida", Host = "h", Senha = "x" });
        var r = Validar(o);
        Assert.Single(r.Configuracao!.Cameras);
        Assert.Contains(r.Avisos, a => a.Contains("repetido"));
    }
}

public class FilaLimpezaTests : IDisposable
{
    private readonly PastaTemporaria _pasta = new();
    private readonly RelogioFalso _relogio = new();
    private readonly LogColetor<FilaQuadros> _log = new();

    public void Dispose() => _pasta.Dispose();

    private FilaQuadros Criar(TimeSpan? idade = null, long tamanho = 500L << 20) =>
        new(_pasta.Caminho, new ProtetorFalso(), _relogio, _log, idade ?? TimeSpan.FromHours(24), tamanho);

    private static byte[] Jpeg(byte m, int n = 100) => new byte[] { 0xFF, 0xD8 }.Concat(Enumerable.Repeat(m, n)).Concat(new byte[] { 0xFF, 0xD9 }).ToArray();

    private string Pasta(string camera) => Path.Combine(_pasta.Caminho, "fila", camera);

    [Fact]
    public void LimparApagaQuadrosVencidosDeTodasAsPastasInclusiveDeCameraQueSaiuDaConfiguracao()
    {
        var fila = Criar();
        // Todos dentro das 24 h na hora de gravar (Adicionar já aplica o limite); vencem com o passar do tempo.
        fila.Adicionar("ativa", Jpeg(1), _relogio.Agora.AddHours(-23));
        fila.Adicionar("ativa", Jpeg(2), _relogio.Agora.AddHours(-1));
        // Câmera "fantasma": ninguém mais chama ObterMaisAntigo para ela.
        fila.Adicionar("removida", Jpeg(3), _relogio.Agora.AddHours(-23));

        _relogio.Avancar(TimeSpan.FromHours(2));
        var r = fila.Limpar();

        Assert.Equal(2, r.Expirados); // "ativa"/1 (25 h) e "removida"/3 (25 h)
        Assert.Equal(1, fila.Contar("ativa"));
        Assert.Equal(0, fila.Contar("removida"));
        Assert.False(Directory.Exists(Pasta("removida"))); // pasta vazia some
        Assert.Contains("passaram de", _log.Tudo());
    }

    [Fact]
    public void QuadroSemHorarioNoNomeExpiraPelaDataDeGravacao()
    {
        var fila = Criar();
        fila.Adicionar("c", Jpeg(1), _relogio.Agora); // cria a pasta
        var estranho = Path.Combine(Pasta("c"), "sem-ticks.quadro");
        File.WriteAllBytes(estranho, new byte[] { 1, 2, 3 });
        File.SetLastWriteTimeUtc(estranho, _relogio.Agora.UtcDateTime.AddHours(-30));

        fila.Limpar();
        Assert.False(File.Exists(estranho));
        Assert.Equal(1, fila.Contar("c"));
    }

    [Fact]
    public void LimparApagaTmpOrfaoMaisVelhoQueOLimiteEPreservaOTmpRecente()
    {
        var fila = Criar();
        fila.Adicionar("c", Jpeg(1), _relogio.Agora);
        var velho = Path.Combine(Pasta("c"), "0000000000000000001_000001.quadro.tmp");
        var novo = Path.Combine(Pasta("c"), "0000000000000000002_000002.quadro.tmp");
        File.WriteAllBytes(velho, new byte[10]);
        File.WriteAllBytes(novo, new byte[10]);
        File.SetLastWriteTimeUtc(velho, _relogio.Agora.UtcDateTime.AddMinutes(-6));
        File.SetLastWriteTimeUtc(novo, _relogio.Agora.UtcDateTime.AddMinutes(-1));

        var r = fila.Limpar();

        Assert.Equal(1, r.TemporariosApagados);
        Assert.False(File.Exists(velho));
        Assert.True(File.Exists(novo));
        Assert.Contains("temporário", _log.Tudo());
    }

    [Fact]
    public void TmpContaNoCalculoDeEspacoEOLimiteApagaQuadrosMaisAntigosParaCompensar()
    {
        var fila = Criar(tamanho: 12 * 1024);
        fila.Adicionar("c", Jpeg(1, 5000), _relogio.Agora.AddMinutes(-4)); // ~5 KB
        fila.Adicionar("c", Jpeg(2, 5000), _relogio.Agora.AddMinutes(-3)); // ~5 KB (cabe: 10 KB)
        Assert.Equal(2, fila.Contar("c"));

        // Um .tmp recente (pode ser gravação em andamento) de 5 KB passa o total de 12 KB.
        var tmp = Path.Combine(Pasta("c"), "0000000000000000009_000009.quadro.tmp");
        File.WriteAllBytes(tmp, new byte[5000]);
        File.SetLastWriteTimeUtc(tmp, _relogio.Agora.UtcDateTime.AddMinutes(-1));
        Assert.True(fila.TamanhoTotalBytes() > 12 * 1024);

        var r = fila.Limpar();

        Assert.Equal(1, r.ExcedenteTamanho);
        Assert.Equal(1, fila.Contar("c"));
        Assert.True(r.TamanhoBytes <= 12 * 1024);
        Assert.Equal(2, fila.ObterMaisAntigo("c")!.Jpeg[2]); // saiu o mais antigo
    }

    [Fact]
    public void LimparAplicaOLimiteDeTamanhoEntreTodasAsCameras()
    {
        var fila = Criar(tamanho: 100 * 1024);
        var t = _relogio.Agora;
        for (var i = 0; i < 6; i++) fila.Adicionar("a", Jpeg((byte)i, 5000), t.AddMinutes(-60 + i));
        // Reabre com limite menor (ex.: configuração reduzida) e limpa.
        var menor = Criar(tamanho: 12 * 1024);
        var r = menor.Limpar();
        Assert.True(r.ExcedenteTamanho >= 3);
        Assert.True(menor.TamanhoTotalBytes() <= 12 * 1024);
    }
}

public class LimpezaPeriodicaTests : IDisposable
{
    private readonly PastaTemporaria _pasta = new();
    private readonly RelogioFalso _relogio = new();
    private readonly LogColetor _log = new();

    public void Dispose() => _pasta.Dispose();

    private FilaQuadros Fila() => new(_pasta.Caminho, new ProtetorFalso(), _relogio, new LogColetor<FilaQuadros>(), TimeSpan.FromHours(24), 500L << 20);

    private static byte[] Jpeg(byte m) => new byte[] { 0xFF, 0xD8, m, 0xFF, 0xD9 };

    [Fact]
    public async Task RodaNaPartidaEDepoisACada10MinutosSemDependerDeNenhumEnviador()
    {
        var fila = Fila();
        // Fila de uma câmera que nunca teve enviador (ex.: removida da configuração), já vencida.
        fila.Adicionar("fantasma", Jpeg(1), _relogio.Agora.AddHours(-30));
        var limpeza = new LimpezaPeriodica(fila, _relogio, _log);

        using var cts = new CancellationTokenSource();
        var tarefa = Task.Run(() => limpeza.ExecutarAsync(cts.Token));
        var limite = DateTime.UtcNow.AddSeconds(5);
        while (limpeza.Execucoes < 3 && DateTime.UtcNow < limite) await Task.Delay(10);
        cts.Cancel();
        await tarefa.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(limpeza.Execucoes >= 3);
        Assert.Equal(0, fila.Contar("fantasma")); // limpou na partida
        Assert.All(_relogio.Atrasos, a => Assert.Equal(TimeSpan.FromMinutes(10), a));
    }

    [Fact]
    public async Task QuadrosQueVencemComOTempoSaemNaProximaRodadaMesmoComEnvioPausadoPor401()
    {
        var fila = Fila();
        var estado = new EstadoApi();
        estado.PausarAte(_relogio.Agora.AddDays(5)); // pausa por 401: o enviador não toca na fila
        var enviador = new EnviadorCamera(Modelos.Camera("cam-1"), new ClienteFalso(), fila, estado, _relogio, _log);
        fila.Adicionar("cam-1", Jpeg(1), _relogio.Agora);
        Assert.NotNull(await enviador.PassoAsync(CancellationToken.None)); // pausado: só espera

        var limpeza = new LimpezaPeriodica(fila, _relogio, _log);
        limpeza.ExecutarUmaVez();
        Assert.Equal(1, fila.Contar("cam-1")); // ainda dentro de 24 h

        _relogio.Avancar(TimeSpan.FromHours(24) + TimeSpan.FromMinutes(1));
        var r = limpeza.ExecutarUmaVez();
        Assert.Equal(1, r!.Expirados);
        Assert.Equal(0, fila.Contar("cam-1"));
    }

    [Fact]
    public void ArquivoTravadoNaLimpezaNaoDerrubaOLacoESegueComOsDemais()
    {
        var fila = Fila();
        fila.Adicionar("c", Jpeg(1), _relogio.Agora.AddHours(-23));
        fila.Adicionar("c", Jpeg(2), _relogio.Agora.AddHours(-22));
        _relogio.Avancar(TimeSpan.FromHours(10)); // os dois vencem
        var arquivos = Directory.GetFiles(Path.Combine(_pasta.Caminho, "fila", "c"), "*.quadro").OrderBy(f => f, StringComparer.Ordinal).ToArray();

        // O primeiro arquivo vencido fica aberto por outro processo: não dá para apagar.
        using var travado = new FileStream(arquivos[0], FileMode.Open, FileAccess.Read, FileShare.None);
        var limpeza = new LimpezaPeriodica(fila, _relogio, _log);
        var r = limpeza.ExecutarUmaVez(); // não lança

        Assert.NotNull(r);
        Assert.False(File.Exists(arquivos[1])); // o outro foi apagado
        Assert.True(File.Exists(arquivos[0]));
    }
}

public class SupervisorTests
{
    private readonly RelogioFalso _relogio = new();
    private readonly LogColetor _log = new();

    [Fact]
    public async Task TarefaQueTerminaSemParadaELogadaEReiniciada()
    {
        var execucoes = 0;
        using var cts = new CancellationTokenSource();
        await Supervisor.ExecutarAsync("teste", ct =>
        {
            if (Interlocked.Increment(ref execucoes) >= 3) cts.Cancel();
            return Task.CompletedTask; // termina "em silêncio"
        }, _relogio, _log, cts.Token, TimeSpan.FromSeconds(30));

        Assert.Equal(3, execucoes);
        Assert.Contains(_log.Linhas, l => l.Nivel == LogLevel.Error && l.Texto.Contains("terminou sem que o serviço fosse parado"));
        Assert.Contains(TimeSpan.FromSeconds(30), _relogio.Atrasos);
    }

    [Fact]
    public async Task TarefaQueLancaExcecaoELogadaEReiniciada()
    {
        var execucoes = 0;
        using var cts = new CancellationTokenSource();
        await Supervisor.ExecutarAsync("teste", ct =>
        {
            if (Interlocked.Increment(ref execucoes) >= 2) cts.Cancel();
            throw new InvalidOperationException("falha qualquer");
        }, _relogio, _log, cts.Token, TimeSpan.FromSeconds(30));

        Assert.Equal(2, execucoes);
        Assert.Contains(_log.Linhas, l => l.Nivel == LogLevel.Error && l.Texto.Contains("falhou de forma inesperada") && l.Texto.Contains("falha qualquer"));
    }

    [Fact]
    public async Task ParadaDoServicoEncerraSemLogDeErro()
    {
        using var cts = new CancellationTokenSource();
        var tarefa = Supervisor.ExecutarAsync("teste", async ct => await Task.Delay(Timeout.Infinite, ct), _relogio, _log, cts.Token);
        await Task.Delay(50);
        cts.Cancel();
        await tarefa.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.DoesNotContain(_log.Linhas, l => l.Nivel >= LogLevel.Error);
    }

    private int Avisos()
    {
        lock (_log.Linhas) return _log.Linhas.Count(l => l.Nivel == LogLevel.Warning && l.Texto.Contains("Nenhuma câmera ativa"));
    }

    [Fact]
    public async Task SemCamerasAtivasAvisaPeriodicamenteACadaHoraEFicaEmEspera()
    {
        using var cts = new CancellationTokenSource();
        var tarefa = Task.Run(() => Supervisor.AvisarSemCamerasAsync(_relogio, _log, cts.Token));
        var limite = DateTime.UtcNow.AddSeconds(5);
        while (Avisos() < 3 && DateTime.UtcNow < limite) await Task.Delay(10);
        Assert.False(tarefa.IsCompleted); // continua de pé
        cts.Cancel();
        await tarefa.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(Avisos() >= 3);
        Assert.All(_relogio.Atrasos, a => Assert.Equal(TimeSpan.FromHours(1), a));
    }
}

public class PermissaoPastaTests : IDisposable
{
    private readonly PastaTemporaria _pasta = new();
    public void Dispose() => _pasta.Dispose();

    // Pasta com ACL própria, sem herança: só o usuário atual (independe do que o %TEMP% da máquina herda).
    private string PastaRestrita()
    {
        var info = new DirectoryInfo(_pasta.Caminho);
        var acl = info.GetAccessControl();
        acl.SetAccessRuleProtection(true, false);
        foreach (FileSystemAccessRule r in acl.GetAccessRules(true, false, typeof(SecurityIdentifier))) acl.RemoveAccessRuleAll(r);
        acl.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        info.SetAccessControl(acl);
        return _pasta.Caminho;
    }

    private static void Conceder(string pasta, WellKnownSidType sid, FileSystemRights direitos, PropagationFlags propagacao = PropagationFlags.None)
    {
        var info = new DirectoryInfo(pasta);
        var acl = info.GetAccessControl();
        acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid, null), direitos,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, propagacao, AccessControlType.Allow));
        info.SetAccessControl(acl);
    }

    [Fact]
    public void PastaRestritaAoUsuarioNaoGeraAviso() => Assert.Null(PermissaoPasta.AvaliarExposicao(PastaRestrita()));

    [Fact]
    public void PastaComLeituraParaUsuariosGeraAvisoComOComandoQueCorrige()
    {
        var pasta = PastaRestrita();
        Conceder(pasta, WellKnownSidType.BuiltinUsersSid, FileSystemRights.ReadAndExecute);
        var aviso = PermissaoPasta.AvaliarExposicao(pasta);
        Assert.NotNull(aviso);
        Assert.Contains("Usuários", aviso);
        Assert.Contains(pasta, aviso);
        Assert.Contains("icacls", aviso);
        Assert.Contains("/inheritance:r", aviso);
    }

    [Fact]
    public void PastaComAcessoParaTodosGeraAviso()
    {
        var pasta = PastaRestrita();
        Conceder(pasta, WellKnownSidType.WorldSid, FileSystemRights.Modify);
        Assert.Contains("Todos", PermissaoPasta.AvaliarExposicao(pasta));
    }

    [Fact]
    public void RegraSoHerdadaPelosFilhosNaoContaParaAPropriaPasta()
    {
        var pasta = PastaRestrita();
        Conceder(pasta, WellKnownSidType.BuiltinUsersSid, FileSystemRights.ReadAndExecute, PropagationFlags.InheritOnly);
        Assert.Null(PermissaoPasta.AvaliarExposicao(pasta));
    }

    [Fact]
    public void PastaInexistenteNaoLancaENaoAvisa() =>
        Assert.Null(PermissaoPasta.AvaliarExposicao(Path.Combine(_pasta.Caminho, "nao-existe")));
}

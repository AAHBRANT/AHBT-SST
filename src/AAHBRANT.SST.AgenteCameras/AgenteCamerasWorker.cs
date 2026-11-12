using AAHBRANT.SST.AgenteCameras.Envio;
using AAHBRANT.SST.AgenteCameras.Fila;
using AAHBRANT.SST.AgenteCameras.Infra;
using AAHBRANT.SST.AgenteCameras.Opcoes;
using AAHBRANT.SST.AgenteCameras.Quadros;
using AAHBRANT.SST.AgenteCameras.Seguranca;
using AAHBRANT.SST.AgenteCameras.Servidor;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.AgenteCameras;

// Mensagens produzidas antes de o log existir (proteção dos arquivos de configuração).
public sealed record MensagensDePartida(IReadOnlyList<string> Informacoes, IReadOnlyList<string> Avisos);

public sealed class AgenteCamerasWorker : BackgroundService
{
    private readonly ResultadoConfiguracao _resultado;
    private readonly MensagensDePartida _partida;
    private readonly Mascarador _mascarador;
    private readonly IProtetorDados _protetor;
    private readonly IRelogio _relogio;
    private readonly ILoggerFactory _fabrica;
    private readonly IHostApplicationLifetime _vida;
    private readonly ILogger<AgenteCamerasWorker> _log;

    public AgenteCamerasWorker(ResultadoConfiguracao resultado, MensagensDePartida partida, Mascarador mascarador,
        IProtetorDados protetor, IRelogio relogio, ILoggerFactory fabrica, IHostApplicationLifetime vida)
    {
        _resultado = resultado;
        _partida = partida;
        _mascarador = mascarador;
        _protetor = protetor;
        _relogio = relogio;
        _fabrica = fabrica;
        _vida = vida;
        _log = fabrica.CreateLogger<AgenteCamerasWorker>();
    }

    protected override async Task ExecuteAsync(CancellationToken parada)
    {
        // Libera o host imediatamente: a partida do serviço não espera a inicialização das câmeras.
        await Task.Yield();

        var versao = typeof(AgenteCamerasWorker).Assembly.GetName().Version;
        _log.LogInformation("AAHBRANT SST - Agente de Câmeras {Versao} iniciando.", versao);
        foreach (var i in _partida.Informacoes) _log.LogInformation("{Mensagem}", i);
        foreach (var a in _partida.Avisos) _log.LogWarning("{Mensagem}", a);
        foreach (var a in _resultado.Avisos) _log.LogWarning("{Mensagem}", a);

        if (!_resultado.Valida)
        {
            foreach (var e in _resultado.Erros) _log.LogError("Configuração inválida: {Erro}", e);
            _log.LogError("O serviço não pode iniciar com esta configuração. Corrija o arquivo de configuração; o serviço tenta de novo ao reiniciar.");
            Environment.ExitCode = 1;
            _vida.StopApplication();
            return;
        }

        var config = _resultado.Configuracao!;
        _mascarador.Registrar(config.Segredo);
        foreach (var c in config.Cameras) _mascarador.Registrar(c.Senha);

        FilaQuadros fila;
        try
        {
            Directory.CreateDirectory(config.PastaDados);
            fila = new FilaQuadros(config.PastaDados, _protetor, _relogio, _fabrica.CreateLogger<FilaQuadros>(),
                config.FilaIdadeMaxima, config.FilaTamanhoMaximoBytes);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _log.LogError("Não foi possível usar a pasta de dados \"{Pasta}\": {Erro}", config.PastaDados, e.Message);
            Environment.ExitCode = 1;
            _vida.StopApplication();
            return;
        }

        _log.LogInformation("{N} câmera(s) ativa(s) no arquivo local{Servidor}; API {Api}; pasta de dados {Pasta}; fila offline até {Horas:0.#} h / {Mb} MB.",
            config.Cameras.Count, config.LerConfiguracaoDoServidor ? " (mais as do cadastro no servidor)" : "", config.ApiBaseUrl, config.PastaDados, config.FilaIdadeMaxima.TotalHours, config.FilaTamanhoMaximoBytes / (1024 * 1024));

        if (PermissaoPasta.AvaliarExposicao(config.PastaDados) is { } exposicao)
            _log.LogWarning("{Mensagem}", exposicao);

        using var http = new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectTimeout = TimeSpan.FromSeconds(15),
        })
        { Timeout = Timeout.InfiniteTimeSpan }; // o limite de 90 s por envio é aplicado em ClienteQuadros

        var cliente = new ClienteQuadros(http, config.ApiBaseUrl, config.AgenteId, config.Segredo, _mascarador);
        var fonte = new FfmpegFonteQuadros(config.CaminhoFfmpeg, _mascarador, _fabrica.CreateLogger<FfmpegFonteQuadros>());
        var amostrador = new AmostradorGdi();
        var estado = new EstadoApi();

        // Toda tarefa roda sob o Supervisor: se terminar ou falhar sem o serviço ter sido parado, é registrada e
        // reiniciada. Nenhuma câmera é desligada "para sempre" (404 apenas pausa a câmera por um tempo).
        var logLimpeza = _fabrica.CreateLogger("LimpezaFila");
        var limpeza = new LimpezaPeriodica(fila, _relogio, logLimpeza);
        var tarefas = new List<Task>
        {
            // Na partida e a cada 10 min, com ou sem câmeras ativas, mesmo com envio pausado.
            Task.Run(() => Supervisor.ExecutarAsync("limpeza da fila", limpeza.ExecutarAsync, _relogio, logLimpeza, parada), CancellationToken.None),
        };

        // Leitura + envio de UMA câmera. O reconciliador chama isto para cada câmera que precisa estar no ar e cancela o
        // token dela ao parar ou reiniciar (mudança de cadastro), sem afetar as outras.
        Task ExecutarCamera(CameraConfig camera, CancellationToken ctCamera)
        {
            var log = _fabrica.CreateLogger("Camera");
            var enviador = new EnviadorCamera(camera, cliente, fila, estado, _relogio, log);
            var monitor = new MonitorCamera(camera, fonte, amostrador, enviador, _relogio, _mascarador, log);
            return Task.WhenAll(
                Task.Run(() => Supervisor.ExecutarAsync($"envio {camera.Nome}", enviador.ExecutarAsync, _relogio, log, ctCamera), CancellationToken.None),
                Task.Run(() => Supervisor.ExecutarAsync($"leitura {camera.Nome}", monitor.ExecutarAsync, _relogio, log, ctCamera), CancellationToken.None));
        }

        var reconciliador = new ReconciliadorCameras(ExecutarCamera, parada, _fabrica.CreateLogger("Reconciliador"));
        ProcessadorTestesConexao? testes = null;

        if (config.LerConfiguracaoDoServidor)
        {
            // A consulta da configuração é o sinal de vida do agente e traz a lista de câmeras e os testes pedidos pela tela.
            var logServidor = _fabrica.CreateLogger("Configuracao");
            var clienteConfig = new ClienteConfiguracao(http, config.ApiBaseUrl, config.AgenteId, config.Segredo, _mascarador);
            testes = new ProcessadorTestesConexao(fonte, clienteConfig, _mascarador, _relogio, _fabrica.CreateLogger("TesteConexao"), parada);
            var cache = new CacheCameras(config.PastaDados, _protetor, config.AgenteId, logServidor);
            var sincronizador = new SincronizadorConfiguracao(clienteConfig, reconciliador, testes, cache, config.Cameras, estado, _relogio, _mascarador, logServidor);
            tarefas.Add(Task.Run(() => Supervisor.ExecutarAsync("configuração do servidor", sincronizador.ExecutarAsync, _relogio, logServidor, parada), CancellationToken.None));
        }
        else
        {
            _log.LogInformation("Leitura da configuração no servidor desligada (AgenteCameras:LerConfiguracaoDoServidor=false): só as câmeras deste arquivo são usadas.");
            await reconciliador.ReconciliarAsync(config.Cameras).ConfigureAwait(false);
            if (config.Cameras.Count == 0)
                tarefas.Add(Task.Run(() => Supervisor.AvisarSemCamerasAsync(_relogio, _log, parada), CancellationToken.None));
        }

        await Task.WhenAll(tarefas).ConfigureAwait(false);
        await reconciliador.AguardarTodasAsync().ConfigureAwait(false);
        if (testes is not null) await testes.AguardarTodosAsync().ConfigureAwait(false);

        if (!parada.IsCancellationRequested)
        {
            // Não deveria acontecer (as tarefas só terminam com a parada). Se acontecer, não fica "em execução" sem
            // fazer nada: encerra com erro para o Windows reiniciar o serviço (recuperação configurada no instalar.ps1).
            _log.LogCritical("As tarefas do agente terminaram sem o serviço ter sido parado; encerrando com erro para o Windows reiniciar o serviço.");
            Environment.ExitCode = 1;
            _vida.StopApplication();
            return;
        }
        _log.LogInformation("Agente de Câmeras encerrado.");
    }
}

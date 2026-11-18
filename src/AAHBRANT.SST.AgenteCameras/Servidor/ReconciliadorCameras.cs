using AAHBRANT.SST.AgenteCameras.Opcoes;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.AgenteCameras.Servidor;

public sealed record ResumoReconciliacao(
    IReadOnlyList<string> Iniciadas,
    IReadOnlyList<string> Paradas,
    IReadOnlyList<string> Reiniciadas,
    IReadOnlyList<string> Mantidas)
{
    public bool HouveMudanca => Iniciadas.Count + Paradas.Count + Reiniciadas.Count > 0;
}

// Mantém em execução exatamente as câmeras desejadas. Cada câmera roda na própria tarefa (e no próprio token),
// então iniciar, parar ou reiniciar uma nunca encosta nas outras nem no envio da fila offline. Câmera sem mudança
// na leitura (host, porta, usuário, senha, canal, stream, modelo ou intervalo) NÃO é reiniciada; mudar só o
// nome também não reinicia (o nome só aparece no log e vale na próxima religação).
public sealed class ReconciliadorCameras
{
    public static readonly TimeSpan EsperaParadaPadrao = TimeSpan.FromSeconds(15);

    private sealed class Execucao
    {
        public Execucao(CameraConfig config, CancellationTokenSource cts, Task tarefa)
        {
            Config = config;
            Cts = cts;
            Tarefa = tarefa;
        }

        public CameraConfig Config { get; }
        public CancellationTokenSource Cts { get; }
        public Task Tarefa { get; }
    }

    private readonly Func<CameraConfig, CancellationToken, Task> _executar;
    private readonly CancellationToken _parada;
    private readonly ILogger _log;
    private readonly TimeSpan _esperaParada;
    private readonly SemaphoreSlim _trava = new(1, 1);
    private readonly Dictionary<string, Execucao> _execucoes = new(StringComparer.OrdinalIgnoreCase);

    public ReconciliadorCameras(Func<CameraConfig, CancellationToken, Task> executarCamera, CancellationToken parada, ILogger log, TimeSpan? esperaParada = null)
    {
        _executar = executarCamera;
        _parada = parada;
        _log = log;
        _esperaParada = esperaParada ?? EsperaParadaPadrao;
    }

    /// <summary>Câmeras em execução agora (CameraId -> configuração aplicada).</summary>
    public IReadOnlyDictionary<string, CameraConfig> EmExecucao
    {
        get
        {
            lock (_execucoes) return _execucoes.ToDictionary(p => p.Key, p => p.Value.Config, StringComparer.OrdinalIgnoreCase);
        }
    }

    private static CameraConfig ChaveDeLeitura(CameraConfig c) => c with { Nome = "" };

    public async Task<ResumoReconciliacao> ReconciliarAsync(IEnumerable<CameraConfig> desejadas)
    {
        await _trava.WaitAsync().ConfigureAwait(false);
        try
        {
            var alvo = new Dictionary<string, CameraConfig>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in desejadas) alvo[c.CameraId] = c;

            var iniciadas = new List<string>();
            var paradas = new List<string>();
            var reiniciadas = new List<string>();
            var mantidas = new List<string>();

            string[] atuais;
            lock (_execucoes) atuais = _execucoes.Keys.ToArray();

            foreach (var id in atuais.Where(id => !alvo.ContainsKey(id)))
            {
                await PararAsync(id).ConfigureAwait(false);
                paradas.Add(id);
            }

            foreach (var (id, nova) in alvo)
            {
                Execucao? atual;
                lock (_execucoes) _execucoes.TryGetValue(id, out atual);

                if (atual is null)
                {
                    Iniciar(nova);
                    iniciadas.Add(id);
                }
                else if (ChaveDeLeitura(atual.Config) != ChaveDeLeitura(nova))
                {
                    await PararAsync(id).ConfigureAwait(false);
                    Iniciar(nova);
                    reiniciadas.Add(id);
                }
                else
                {
                    mantidas.Add(id);
                }
            }

            return new ResumoReconciliacao(iniciadas, paradas, reiniciadas, mantidas);
        }
        finally
        {
            _trava.Release();
        }
    }

    private void Iniciar(CameraConfig config)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(_parada);
        // Sem Task.Run: a câmera começa a rodar de forma síncrona até o primeiro await (o que o delegado faz antes
        // disso é só montar objetos), então "iniciada" significa mesmo iniciada quando este método devolve.
        var tarefa = ExecutarComSeguranca(config, cts.Token);
        lock (_execucoes) _execucoes[config.CameraId] = new Execucao(config, cts, tarefa);
    }

    private async Task ExecutarComSeguranca(CameraConfig config, CancellationToken ct)
    {
        try { await _executar(config, ct).ConfigureAwait(false); }
        catch (OperationCanceledException) { /* parada pedida */ }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _log.LogError(e, "[{Camera}] a execução da câmera terminou com erro inesperado.", config.Nome);
        }
    }

    private async Task PararAsync(string id)
    {
        Execucao? e;
        lock (_execucoes)
        {
            if (!_execucoes.Remove(id, out e)) return;
        }
        try { e.Cts.Cancel(); } catch (ObjectDisposedException) { }
        try
        {
            await e.Tarefa.WaitAsync(_esperaParada).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            _log.LogWarning("[{Camera}] a câmera demorou mais de {Segundos:0} s para parar; segue encerrando em segundo plano.", e.Config.Nome, _esperaParada.TotalSeconds);
            return; // não descarta o token: a tarefa ainda pode estar usando
        }
        e.Cts.Dispose();
    }

    /// <summary>Espera todas as câmeras terminarem (usar depois de a parada do serviço ser pedida).</summary>
    public async Task AguardarTodasAsync()
    {
        Task[] tarefas;
        lock (_execucoes) tarefas = _execucoes.Values.Select(x => x.Tarefa).ToArray();
        try { await Task.WhenAll(tarefas).WaitAsync(_esperaParada).ConfigureAwait(false); }
        catch (TimeoutException) { _log.LogWarning("Algumas câmeras não encerraram dentro de {Segundos:0} s.", _esperaParada.TotalSeconds); }
    }
}

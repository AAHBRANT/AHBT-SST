using AAHBRANT.SST.AgenteCameras.Opcoes;
using AAHBRANT.SST.AgenteCameras.Servidor;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.AgenteCameras.AoVivo;

public sealed record ResumoAoVivo(
    IReadOnlyList<string> Iniciadas,
    IReadOnlyList<string> Paradas,
    IReadOnlyList<string> Reiniciadas,
    IReadOnlyList<string> Mantidas)
{
    public bool HouveMudanca => Iniciadas.Count + Paradas.Count + Reiniciadas.Count > 0;
}

// Reconcilia as sessões ao vivo recebidas na configuração (mesmo padrão do ReconciliadorCameras): inicia as novas e
// para as que sumiram. Cada sessão roda na própria tarefa e no próprio ffmpeg; nada aqui encosta no ReconciliadorCameras
// nem nas câmeras da análise. Sessão cuja câmera não está em "cameras" é ignorada (só se transmite câmera cadastrada).
//
// Uma transmissão que terminou sozinha (falha, 410, prazo) NÃO é religada enquanto a sessão continuar na lista: a API
// a encerra e ela some; se a tela pedir de novo, nasce uma sessão nova.
public sealed class GerenciadorAoVivo
{
    public static readonly TimeSpan EsperaParadaPadrao = TimeSpan.FromSeconds(15);

    // Teto próprio do agente (a API já limita em 2 por agente, mas cliques simultâneos podem passar por ela): além disto, ignora.
    public const int MaximoTransmissoesSimultaneas = 2;

    private sealed class Execucao
    {
        public Execucao(SessaoAoVivoServidor sessao, CameraConfig cameraDaAnalise, TransmissaoAoVivo transmissao, CancellationTokenSource cts, Task tarefa)
        {
            Sessao = sessao;
            CameraDaAnalise = cameraDaAnalise;
            Transmissao = transmissao;
            Cts = cts;
            Tarefa = tarefa;
        }

        public SessaoAoVivoServidor Sessao { get; }
        public CameraConfig CameraDaAnalise { get; }
        public TransmissaoAoVivo Transmissao { get; }
        public CancellationTokenSource Cts { get; }
        public Task Tarefa { get; }
    }

    private readonly Func<SessaoAoVivoServidor, CameraConfig, TransmissaoAoVivo> _fabrica;
    private readonly CancellationToken _parada;
    private readonly ILogger _log;
    private readonly TimeSpan _esperaParada;
    private readonly SemaphoreSlim _trava = new(1, 1);
    private readonly Dictionary<Guid, Execucao> _execucoes = new();

    public GerenciadorAoVivo(Func<SessaoAoVivoServidor, CameraConfig, TransmissaoAoVivo> fabrica, CancellationToken parada, ILogger log,
        TimeSpan? esperaParada = null)
    {
        _fabrica = fabrica;
        _parada = parada;
        _log = log;
        _esperaParada = esperaParada ?? EsperaParadaPadrao;
    }

    /// <summary>Sessões com transmissão registrada agora (inclui as que já terminaram e ainda estão na lista da API).</summary>
    public IReadOnlyList<Guid> EmExecucao
    {
        get
        {
            lock (_execucoes) return _execucoes.Keys.ToList();
        }
    }

    /// <summary>Transmissão da sessão (para diagnóstico e testes), ou null.</summary>
    public TransmissaoAoVivo? Transmissao(Guid sessaoId)
    {
        lock (_execucoes) return _execucoes.TryGetValue(sessaoId, out var e) ? e.Transmissao : null;
    }

    private static CameraConfig ChaveDeLeitura(CameraConfig c) => c with { Nome = "" };

    private static SessaoAoVivoServidor Normalizar(SessaoAoVivoServidor s) => s with
    {
        Fps = Math.Clamp(s.Fps, 1, TransmissaoAoVivo.FpsMaximo),
        LarguraMaxima = s.LarguraMaxima <= 0 ? TransmissaoAoVivo.LarguraPadrao : Math.Clamp(s.LarguraMaxima, TransmissaoAoVivo.LarguraMinima, TransmissaoAoVivo.LarguraMaxima),
    };

    // O que muda a transmissão em curso: a câmera (host, porta, usuário, senha, canal...) e o formato pedido.
    private static bool MudouFormato(SessaoAoVivoServidor a, SessaoAoVivoServidor b) =>
        a.Fps != b.Fps || a.LarguraMaxima != b.LarguraMaxima || a.CameraId != b.CameraId;

    public async Task<ResumoAoVivo> ReconciliarAsync(IEnumerable<SessaoAoVivoServidor> sessoes, IReadOnlyDictionary<string, CameraConfig> cameras)
    {
        await _trava.WaitAsync().ConfigureAwait(false);
        try
        {
            var alvo = new Dictionary<Guid, (SessaoAoVivoServidor Sessao, CameraConfig Camera)>();
            // As que já têm transmissão registrada vêm primeiro (não troca uma em curso por outra); depois a ordem da API (mais antigas).
            HashSet<Guid> emCurso;
            lock (_execucoes) emCurso = _execucoes.Keys.ToHashSet();
            var ordenadas = sessoes.OrderBy(s => emCurso.Contains(s.SessaoId) ? 0 : 1).ToList(); // OrderBy é estável
            foreach (var bruta in ordenadas)
            {
                if (bruta.SessaoId == Guid.Empty || bruta.CameraId == Guid.Empty) continue;
                if (bruta.SegundosRestantes <= 0) continue; // já venceu: a API a encerra
                if (!cameras.TryGetValue(bruta.CameraId.ToString("D"), out var camera))
                {
                    _log.LogWarning("[ao vivo {Sessao}] a câmera {Camera} não está ativa neste agente; sessão ignorada.", bruta.SessaoId, bruta.CameraId);
                    continue;
                }
                if (!alvo.ContainsKey(bruta.SessaoId) && alvo.Count >= MaximoTransmissoesSimultaneas)
                {
                    _log.LogWarning("[ao vivo {Sessao}] o agente já transmite {Maximo} câmeras ao vivo; sessão ignorada.", bruta.SessaoId, MaximoTransmissoesSimultaneas);
                    continue;
                }
                alvo[bruta.SessaoId] = (Normalizar(bruta), camera);
            }

            var iniciadas = new List<string>();
            var paradas = new List<string>();
            var reiniciadas = new List<string>();
            var mantidas = new List<string>();

            Guid[] atuais;
            lock (_execucoes) atuais = _execucoes.Keys.ToArray();

            foreach (var id in atuais.Where(id => !alvo.ContainsKey(id)))
            {
                await PararAsync(id, MotivoParadaAoVivo.SumiuDaConfiguracao).ConfigureAwait(false);
                paradas.Add(id.ToString("D"));
            }

            foreach (var (id, (sessao, camera)) in alvo)
            {
                Execucao? atual;
                lock (_execucoes) _execucoes.TryGetValue(id, out atual);

                if (atual is null)
                {
                    Iniciar(sessao, camera);
                    iniciadas.Add(id.ToString("D"));
                }
                else if (atual.Transmissao.Terminou)
                {
                    mantidas.Add(id.ToString("D")); // terminou sozinha: não religa (a API encerra a sessão)
                }
                else if (MudouFormato(atual.Sessao, sessao) || ChaveDeLeitura(atual.CameraDaAnalise) != ChaveDeLeitura(camera))
                {
                    await PararAsync(id, MotivoParadaAoVivo.SumiuDaConfiguracao).ConfigureAwait(false);
                    Iniciar(sessao, camera);
                    reiniciadas.Add(id.ToString("D"));
                }
                else
                {
                    atual.Transmissao.AtualizarPrazo(sessao.SegundosRestantes);
                    mantidas.Add(id.ToString("D"));
                }
            }

            return new ResumoAoVivo(iniciadas, paradas, reiniciadas, mantidas);
        }
        finally
        {
            _trava.Release();
        }
    }

    private void Iniciar(SessaoAoVivoServidor sessao, CameraConfig camera)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(_parada);
        TransmissaoAoVivo transmissao;
        Task tarefa;
        try
        {
            transmissao = _fabrica(sessao, camera);
            tarefa = ExecutarComSeguranca(transmissao, cts.Token);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _log.LogError(e, "[ao vivo {Sessao}] não foi possível iniciar a transmissão.", sessao.SessaoId);
            cts.Dispose();
            return;
        }
        lock (_execucoes) _execucoes[sessao.SessaoId] = new Execucao(sessao, camera, transmissao, cts, tarefa);
    }

    private async Task ExecutarComSeguranca(TransmissaoAoVivo transmissao, CancellationToken ct)
    {
        try { await transmissao.ExecutarAsync(ct).ConfigureAwait(false); }
        catch (OperationCanceledException) { /* parada pedida */ }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _log.LogError(e, "[ao vivo {Sessao}] a transmissão terminou com erro inesperado.", transmissao.Sessao.SessaoId);
        }
    }

    private async Task PararAsync(Guid id, MotivoParadaAoVivo motivo)
    {
        Execucao? e;
        lock (_execucoes)
        {
            if (!_execucoes.Remove(id, out e)) return;
        }
        e.Transmissao.Parar(motivo);
        try { e.Cts.Cancel(); } catch (ObjectDisposedException) { }
        try
        {
            await e.Tarefa.WaitAsync(_esperaParada).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            _log.LogWarning("[ao vivo {Sessao}] a transmissão demorou mais de {Segundos:0} s para parar; segue encerrando em segundo plano.", id, _esperaParada.TotalSeconds);
            return; // não descarta o token: a tarefa ainda pode estar usando
        }
        e.Cts.Dispose();
    }

    /// <summary>Espera todas as transmissões terminarem (usar depois de a parada do serviço ser pedida).</summary>
    public async Task AguardarTodasAsync()
    {
        Task[] tarefas;
        lock (_execucoes) tarefas = _execucoes.Values.Select(x => x.Tarefa).ToArray();
        try { await Task.WhenAll(tarefas).WaitAsync(_esperaParada).ConfigureAwait(false); }
        catch (TimeoutException) { _log.LogWarning("Algumas transmissões ao vivo não encerraram dentro de {Segundos:0} s.", _esperaParada.TotalSeconds); }
    }
}

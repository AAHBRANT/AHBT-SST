using AAHBRANT.SST.AgenteCameras.Envio;
using AAHBRANT.SST.AgenteCameras.Infra;
using AAHBRANT.SST.AgenteCameras.Opcoes;
using AAHBRANT.SST.AgenteCameras.Seguranca;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.AgenteCameras.Quadros;

// Supervisor de UMA câmera: abre o fluxo, aplica a política de envio a cada quadro e entrega ao EnviadorCamera.
// Vigia: mais de 20 s sem quadro encerra a leitura e religa com espera crescente (2, 4, 8... até 60 s);
// religa de qualquer forma a cada 30 min (o stream dessa câmera expira). NUNCA desiste. Uma câmera com
// problema não afeta as outras: cada uma roda no próprio supervisor, e toda exceção aqui vira "religar".
public sealed class MonitorCamera
{
    public static readonly TimeSpan SemQuadros = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan ReligarPeriodico = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan EsperaFfmpegAusente = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan PassoVigia = TimeSpan.FromSeconds(1);

    private readonly CameraConfig _camera;
    private readonly IFonteQuadros _fonte;
    private readonly IAmostrador _amostrador;
    private readonly EnviadorCamera _enviador;
    private readonly IRelogio _relogio;
    private readonly Mascarador _mascarador;
    private readonly ILogger _log;
    private readonly PoliticaEnvio _politica;

    public MonitorCamera(CameraConfig camera, IFonteQuadros fonte, IAmostrador amostrador, EnviadorCamera enviador,
        IRelogio relogio, Mascarador mascarador, ILogger log)
    {
        _camera = camera;
        _fonte = fonte;
        _amostrador = amostrador;
        _enviador = enviador;
        _relogio = relogio;
        _mascarador = mascarador;
        _log = log;
        _politica = new PoliticaEnvio(camera.IntervaloEnvio);
    }

    /// <summary>Quantidade de religamentos feitos (para diagnóstico e testes).</summary>
    public int Sessoes { get; private set; }

    public async Task ExecutarAsync(CancellationToken ct)
    {
        var tentativa = 0;
        while (!ct.IsCancellationRequested)
        {
            Sessoes++;
            _politica.Reiniciar();
            var sessao = new Sessao(_relogio.Agora);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var vigia = VigiarAsync(sessao, cts);
            string motivo;
            var espera = TimeSpan.Zero;
            try
            {
                _log.LogInformation("[{Camera}] abrindo o fluxo de vídeo (tentativa {N}).", _camera.Nome, Sessoes);
                await foreach (var jpeg in _fonte.LerQuadrosAsync(_camera, cts.Token).WithCancellation(cts.Token))
                {
                    sessao.Recebeu(_relogio.Agora);
                    ProcessarQuadro(jpeg);
                }
                motivo = "o fluxo terminou";
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                motivo = sessao.MotivoVigia ?? "leitura cancelada";
            }
            catch (FfmpegIndisponivelException e)
            {
                motivo = e.Message;
                espera = EsperaFfmpegAusente;
                tentativa = 0;
                _log.LogError("[{Camera}] {Motivo}", _camera.Nome, _mascarador.Mascarar(e.Message));
            }
            catch (Exception e)
            {
                motivo = _mascarador.Mascarar(e.Message);
            }
            finally
            {
                cts.Cancel();
                await vigia.ConfigureAwait(false);
            }

            if (espera == TimeSpan.Zero)
            {
                if (sessao.ReligarPlanejado)
                {
                    tentativa = 0;
                    espera = TimeSpan.FromSeconds(1);
                    _log.LogInformation("[{Camera}] religando o fluxo (renovação periódica de {Minutos:0} min).", _camera.Nome, ReligarPeriodico.TotalMinutes);
                }
                else
                {
                    // Sessão que chegou a entregar quadros recomeça a escala do 2 s; sem quadros, a espera cresce.
                    tentativa = sessao.QuadrosRecebidos > 0 ? 1 : tentativa + 1;
                    espera = EsperaCrescente.Calcular(tentativa);
                    _log.LogWarning("[{Camera}] fluxo interrompido: {Motivo}. Religando em {Segundos:0} s.", _camera.Nome, _mascarador.Mascarar(motivo), espera.TotalSeconds);
                }
            }

            try { await _relogio.AtrasarAsync(espera, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    internal void ProcessarQuadro(byte[] jpeg)
    {
        var agora = _relogio.Agora;
        var amostra = _amostrador.Amostrar(jpeg);
        if (amostra is null)
        {
            _log.LogDebug("[{Camera}] quadro ilegível ignorado.", _camera.Nome);
            return;
        }
        if (!_politica.DeveEnviar(amostra, agora)) return;
        _enviador.OferecerVivo(LimitadorTamanho.Garantir(jpeg), agora);
    }

    private async Task VigiarAsync(Sessao sessao, CancellationTokenSource cts)
    {
        try
        {
            while (!cts.IsCancellationRequested)
            {
                await _relogio.AtrasarAsync(PassoVigia, cts.Token).ConfigureAwait(false);
                var agora = _relogio.Agora;
                if (agora - sessao.UltimoQuadro > SemQuadros)
                {
                    sessao.MotivoVigia = $"sem quadros há mais de {SemQuadros.TotalSeconds:0} s";
                    cts.Cancel();
                    return;
                }
                if (agora - sessao.Inicio >= ReligarPeriodico)
                {
                    sessao.MotivoVigia = "renovação periódica";
                    sessao.ReligarPlanejado = true;
                    cts.Cancel();
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // sessão terminou
        }
    }

    private sealed class Sessao
    {
        private long _ultimoQuadroTicks;

        public Sessao(DateTimeOffset inicio)
        {
            Inicio = inicio;
            _ultimoQuadroTicks = inicio.UtcTicks;
        }

        public DateTimeOffset Inicio { get; }
        public DateTimeOffset UltimoQuadro => new(Interlocked.Read(ref _ultimoQuadroTicks), TimeSpan.Zero);
        public int QuadrosRecebidos { get; private set; }
        public volatile string? MotivoVigia;
        public volatile bool ReligarPlanejado;

        public void Recebeu(DateTimeOffset agora)
        {
            Interlocked.Exchange(ref _ultimoQuadroTicks, agora.UtcTicks);
            QuadrosRecebidos++;
        }
    }
}

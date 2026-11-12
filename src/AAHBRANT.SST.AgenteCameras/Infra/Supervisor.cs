using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.AgenteCameras.Infra;

// Garante que nenhuma tarefa de longa duração do serviço termine em silêncio com o host de pé: se ela terminar
// ou falhar sem que o serviço tenha sido parado, registra o erro e a reinicia depois de uma espera.
public static class Supervisor
{
    public static readonly TimeSpan EsperaReinicio = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan IntervaloAvisoSemCameras = TimeSpan.FromHours(1);

    public static async Task ExecutarAsync(string nome, Func<CancellationToken, Task> trabalho, IRelogio relogio, ILogger log,
        CancellationToken parada, TimeSpan? espera = null)
    {
        var pausa = espera ?? EsperaReinicio;
        while (!parada.IsCancellationRequested)
        {
            try
            {
                await trabalho(parada).ConfigureAwait(false);
                if (parada.IsCancellationRequested) break;
                log.LogError("[{Tarefa}] terminou sem que o serviço fosse parado; reiniciando em {Segundos:0} s.", nome, pausa.TotalSeconds);
            }
            catch (OperationCanceledException) when (parada.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                log.LogError(e, "[{Tarefa}] falhou de forma inesperada; reiniciando em {Segundos:0} s.", nome, pausa.TotalSeconds);
            }

            try { await relogio.AtrasarAsync(pausa, parada).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>Sem câmeras ativas o serviço fica de pé em espera; avisa periodicamente para o problema não passar despercebido.</summary>
    public static async Task AvisarSemCamerasAsync(IRelogio relogio, ILogger log, CancellationToken parada, TimeSpan? intervalo = null)
    {
        var cada = intervalo ?? IntervaloAvisoSemCameras;
        while (!parada.IsCancellationRequested)
        {
            try { await relogio.AtrasarAsync(cada, parada).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            log.LogWarning("Nenhuma câmera ativa: o serviço está em espera (nada é lido nem enviado). Confira AgenteCameras:Cameras (CameraId, senha, Habilitada) no arquivo de configuração e reinicie o serviço.");
        }
    }
}

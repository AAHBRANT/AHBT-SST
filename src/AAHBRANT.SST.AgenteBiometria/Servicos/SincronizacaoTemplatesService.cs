using AAHBRANT.SST.AgenteBiometria.Opcoes;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.AgenteBiometria.Servicos;

// Mantém o cache de templates do agente atualizado sem depender de alguém chamar /api/sincronizar:
// sincroniza logo ao iniciar e depois a cada IntervaloSincronizacaoMinutos, para que digitais
// cadastradas há pouco passem a ser reconhecidas. Falha de rede não derruba nada — o cache anterior
// continua valendo e a próxima rodada tenta de novo.
public class SincronizacaoTemplatesService : BackgroundService
{
    private readonly TemplateCacheService _cache;
    private readonly AgenteOptions _options;
    private readonly ILogger<SincronizacaoTemplatesService> _logger;

    public SincronizacaoTemplatesService(
        TemplateCacheService cache, IOptions<AgenteOptions> options, ILogger<SincronizacaoTemplatesService> logger)
    {
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.IntervaloSincronizacaoMinutos <= 0 || _options.DispositivoId == Guid.Empty)
        {
            _logger.LogInformation("Sincronização automática de templates desativada (dispositivo não configurado).");
            return;
        }

        var intervalo = TimeSpan.FromMinutes(_options.IntervaloSincronizacaoMinutos);
        using var timer = new PeriodicTimer(intervalo);

        do
        {
            try
            {
                await _cache.SincronizarAsync(stoppingToken);
                _logger.LogInformation("Templates sincronizados: {Total}.", _cache.Templates.Count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Falha ao sincronizar templates; mantendo o cache anterior.");
            }
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }
}

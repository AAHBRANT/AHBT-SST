using AAHBRANT.SST.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Worker;

// BackgroundService do Motor Central de Alertas (requisito do usuário, 2026-08-24) — processo
// separado da Api para não competir por recursos com as requisições HTTP. Roda em intervalo fixo
// (configurável via "AlertaEngine:IntervaloMinutos", padrão 6h) enquanto o processo estiver de pé.
public class AlertaEngineWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<AlertaEngineWorker> _logger;
    private readonly TimeSpan _intervalo;

    public AlertaEngineWorker(IServiceProvider services, ILogger<AlertaEngineWorker> logger, IConfiguration configuration)
    {
        _services = services;
        _logger = logger;

        var minutos = configuration.GetValue<int?>("AlertaEngine:IntervaloMinutos") ?? 360;
        _intervalo = TimeSpan.FromMinutes(minutos);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var escopo = _services.CreateScope();
                var motor = escopo.ServiceProvider.GetRequiredService<IAlertaEngineService>();
                await motor.ProcessarAsync(stoppingToken);
                _logger.LogInformation("Motor de Alertas processado com sucesso em {Momento}.", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao processar o Motor de Alertas.");
            }

            await Task.Delay(_intervalo, stoppingToken);
        }
    }
}

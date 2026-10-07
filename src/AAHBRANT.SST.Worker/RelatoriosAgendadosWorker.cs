using AAHBRANT.SST.Application.Relatorios;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Worker;

// Relatórios agendados (lista de presença às 08:00 todo dia). A cada poucos minutos pergunta ao
// ExecutarRelatoriosAgendadosCommand, que decide se já é hora e se hoje ainda não foi executado. Assim, se o
// Worker reiniciar às 07:55 ou ficar fora do ar até as 10:00, o disparo acontece quando ele voltar, uma vez só.
public class RelatoriosAgendadosWorker : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(5);

    private readonly IServiceProvider _services;
    private readonly ILogger<RelatoriosAgendadosWorker> _logger;

    public RelatoriosAgendadosWorker(IServiceProvider services, ILogger<RelatoriosAgendadosWorker> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var escopo = _services.CreateScope();
                var publicados = await escopo.ServiceProvider.GetRequiredService<IMediator>()
                    .Send(new ExecutarRelatoriosAgendadosCommand(), stoppingToken);
                if (publicados > 0)
                    _logger.LogInformation("Relatórios agendados: {Quantidade} publicado(s).", publicados);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao executar os relatórios agendados.");
            }

            try
            {
                await Task.Delay(Intervalo, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}

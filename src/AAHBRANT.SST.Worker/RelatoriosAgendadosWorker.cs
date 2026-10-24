using AAHBRANT.SST.Application.Relatorios;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Worker;

// Relatórios agendados (lista de presença às 08:00 todo dia; aviso de itens vencidos às 08:00, seg–sex). A cada poucos minutos pergunta ao
// ExecutarRelatoriosAgendadosCommand, que decide se já é hora e se hoje ainda não foi executado. Assim, se o
// Worker reiniciar às 07:55 ou ficar fora do ar até as 10:00, o disparo acontece quando ele voltar, uma vez só.
public class RelatoriosAgendadosWorker : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(5);

    private readonly IServiceProvider _services;
    private readonly ILogger<RelatoriosAgendadosWorker> _logger;
    private readonly IConfiguration _configuration;

    public RelatoriosAgendadosWorker(
        IServiceProvider services, ILogger<RelatoriosAgendadosWorker> logger, IConfiguration configuration)
    {
        _services = services;
        _logger = logger;
        _configuration = configuration;
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

                // Aviso de itens vencidos das obras (segunda a sexta, 08:00) — escopo próprio para uma falha não afetar o outro.
                // Liga com AvisoDocumentosVencidos:Habilitado=true (prévia em GET /api/alertas/aviso-documentos-vencidos/previa).
                // InicioEm (opcional) segura o primeiro disparo até a data: ligado depois das 08:00, o aviso do dia sairia na
                // hora (ligado pelo usuário em 19/10/2026 às 23h, com início em 20/10).
                var inicioAviso = _configuration.GetValue<DateTime?>("AvisoDocumentosVencidos:InicioEm");
                if (_configuration.GetValue<bool>("AvisoDocumentosVencidos:Habilitado")
                    && (inicioAviso is null || FusoBrasilia.Hoje() >= inicioAviso.Value.Date))
                {
                    using var escopoAviso = _services.CreateScope();
                    var avisos = await escopoAviso.ServiceProvider.GetRequiredService<IMediator>()
                        .Send(new EnviarAvisoDocumentosVencidosCommand(), stoppingToken);
                    if (avisos > 0)
                        _logger.LogInformation("Aviso de itens vencidos: {Quantidade} e-mail(s) enviado(s).", avisos);
                }
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

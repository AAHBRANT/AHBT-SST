using AAHBRANT.SST.Application.Alojamentos.Commands;
using AAHBRANT.SST.Application.Asos.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Integracao.Grh;

// Sincronização periódica de Alojamento e ASO lendo direto do banco do G-RH (Integração G-RH,
// 2026-09-16) — substitui, por enquanto, o padrão de fila de Service Bus usado por Colaborador
// (o G-RH não tinha capacidade de publicar eventos nem construir os endpoints HTTP na hora). Não é
// tempo real de verdade: cada rodada relê o estado atual inteiro do lado do G-RH (mesmo comando de
// upsert usado pela importação manual, ImportarAlojamentosGrhCommand/ImportarAsoGrhCommand),
// portanto o atraso máximo pra uma mudança aparecer no SST é o intervalo configurado
// (GrhDb:IntervaloPollingMinutos, padrão 5 minutos).
public class GrhDbPollingService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly GrhDbOptions _opcoes;
    private readonly ILogger<GrhDbPollingService> _logger;

    public GrhDbPollingService(
        IServiceScopeFactory scopeFactory,
        IOptions<GrhDbOptions> opcoes,
        ILogger<GrhDbPollingService> logger)
    {
        _scopeFactory = scopeFactory;
        _opcoes = opcoes.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = TimeSpan.FromMinutes(Math.Max(1, _opcoes.IntervaloPollingMinutos));
        using var timer = new PeriodicTimer(intervalo);

        do
        {
            await SincronizarUmaRodadaAsync(stoppingToken);
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SincronizarUmaRodadaAsync(CancellationToken ct)
    {
        using var escopo = _scopeFactory.CreateScope();
        var mediator = escopo.ServiceProvider.GetRequiredService<IMediator>();

        try
        {
            var resultadoAlojamentos = await mediator.Send(new ImportarAlojamentosGrhCommand(), ct);
            if (resultadoAlojamentos.Erros.Count > 0)
                _logger.LogWarning(
                    "Polling G-RH (Alojamento): {Sincronizados}/{Recebidos} sincronizados, {Erros} erro(s).",
                    resultadoAlojamentos.TotalSincronizados, resultadoAlojamentos.TotalRecebidos, resultadoAlojamentos.Erros.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha na rodada de polling do G-RH (Alojamento).");
        }

        try
        {
            var resultadoAsos = await mediator.Send(new ImportarAsoGrhCommand(), ct);
            if (resultadoAsos.Erros.Count > 0)
                _logger.LogWarning(
                    "Polling G-RH (ASO): {Sincronizados}/{Recebidos} sincronizados, {Erros} erro(s) (esperado enquanto o cadastro de validade no G-RH está em andamento).",
                    resultadoAsos.TotalSincronizados, resultadoAsos.TotalRecebidos, resultadoAsos.Erros.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha na rodada de polling do G-RH (ASO).");
        }
    }
}

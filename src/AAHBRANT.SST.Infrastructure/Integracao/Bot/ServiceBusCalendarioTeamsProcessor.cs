using System.Text.Json;
using AAHBRANT.SST.Application.Common.Interfaces;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Integracao.Bot;

// Consumidor real da fila Azure Service Bus para sincronização de calendário (mesmo padrão de
// ServiceBusNotificacaoTeamsProcessor — competing consumers, abandona em falha para o Service Bus
// reentregar). A lógica de criar/atualizar/cancelar e o rastreio de estado em CalendarioEventoTeams
// ficam em CalendarioTeamsMensagemHandler, compartilhada com o consumidor em memória.
public class ServiceBusCalendarioTeamsProcessor : BackgroundService
{
    private readonly ServiceBusClient _cliente;
    private readonly ServiceBusOptions _opcoes;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ServiceBusCalendarioTeamsProcessor> _logger;
    private ServiceBusProcessor? _processor;

    public ServiceBusCalendarioTeamsProcessor(
        ServiceBusClient cliente,
        IOptions<ServiceBusOptions> opcoes,
        IServiceScopeFactory scopeFactory,
        ILogger<ServiceBusCalendarioTeamsProcessor> logger)
    {
        _cliente = cliente;
        _opcoes = opcoes.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor = _cliente.CreateProcessor(_opcoes.FilaCalendarioTeams, new ServiceBusProcessorOptions());
        _processor.ProcessMessageAsync += ProcessarMensagemAsync;
        _processor.ProcessErrorAsync += ProcessarErroAsync;

        await _processor.StartProcessingAsync(stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // encerramento normal do host
        }
    }

    private async Task ProcessarMensagemAsync(ProcessMessageEventArgs args)
    {
        var mensagem = JsonSerializer.Deserialize<CalendarioTeamsMensagem>(args.Message.Body.ToString());
        if (mensagem is null)
        {
            await args.DeadLetterMessageAsync(args.Message, "corpo-invalido", cancellationToken: args.CancellationToken);
            return;
        }

        using var escopo = _scopeFactory.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<IAppDbContext>();
        var calendario = escopo.ServiceProvider.GetRequiredService<ICalendarioTeamsService>();

        try
        {
            await CalendarioTeamsMensagemHandler.ProcessarAsync(mensagem, db, calendario, args.CancellationToken);
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Falha ao sincronizar calendário Teams para a origem {Tipo}/{Id} (tentativa {Tentativa}).",
                mensagem.EntidadeOrigemTipo, mensagem.EntidadeOrigemId, args.Message.DeliveryCount);

            await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken);
        }
    }

    private Task ProcessarErroAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception, "Erro no processor do Service Bus (fila de calendário Teams).");
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor is not null)
            await _processor.StopProcessingAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}

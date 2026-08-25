using System.Text.Json;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Integracao.Bot;

// Consumidor real da fila Azure Service Bus (competing consumers — pode rodar tanto na Api quanto no
// Worker ao mesmo tempo sem duplicar envio, o próprio Service Bus distribui as mensagens). Em falha,
// abandona a mensagem em vez de completá-la: o Service Bus reentrega automaticamente até o
// MaxDeliveryCount configurado na fila (provisionamento manual, fora do escopo desta tarefa) e então
// move para a dead-letter queue — é o "reprocessado sem travar a aplicação" de PROJECT RULES.md §4.
public class ServiceBusNotificacaoTeamsProcessor : BackgroundService
{
    private readonly ServiceBusClient _cliente;
    private readonly ServiceBusOptions _opcoes;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ServiceBusNotificacaoTeamsProcessor> _logger;
    private ServiceBusProcessor? _processor;

    public ServiceBusNotificacaoTeamsProcessor(
        ServiceBusClient cliente,
        IOptions<ServiceBusOptions> opcoes,
        IServiceScopeFactory scopeFactory,
        ILogger<ServiceBusNotificacaoTeamsProcessor> logger)
    {
        _cliente = cliente;
        _opcoes = opcoes.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor = _cliente.CreateProcessor(_opcoes.FilaNotificacoesTeams, new ServiceBusProcessorOptions());
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
        var mensagem = JsonSerializer.Deserialize<NotificacaoTeamsMensagem>(args.Message.Body.ToString());
        if (mensagem is null)
        {
            await args.DeadLetterMessageAsync(args.Message, "corpo-invalido", cancellationToken: args.CancellationToken);
            return;
        }

        using var escopo = _scopeFactory.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<IAppDbContext>();
        var notificacao = escopo.ServiceProvider.GetRequiredService<INotificacaoTeamsService>();
        var numeroTentativa = args.Message.DeliveryCount;

        try
        {
            await notificacao.EnviarAsync(
                mensagem.DestinatarioUsuarioId, mensagem.Titulo, mensagem.Descricao, args.CancellationToken);

            db.AlertaHistoricoEnvios.Add(new AlertaHistoricoEnvio
            {
                AlertaId = mensagem.AlertaId,
                Canal = "ActivityFeed",
                Sucesso = true,
                NumeroTentativa = numeroTentativa,
            });
            await db.SaveChangesAsync(args.CancellationToken);
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Falha ao enviar notificação Teams para o usuário {UsuarioId} (tentativa {Tentativa}).",
                mensagem.DestinatarioUsuarioId, numeroTentativa);

            db.AlertaHistoricoEnvios.Add(new AlertaHistoricoEnvio
            {
                AlertaId = mensagem.AlertaId,
                Canal = "ActivityFeed",
                Sucesso = false,
                NumeroTentativa = numeroTentativa,
                MensagemErro = ex.Message,
            });
            await db.SaveChangesAsync(args.CancellationToken);

            await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken);
        }
    }

    private Task ProcessarErroAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception, "Erro no processor do Service Bus (fila de notificações Teams).");
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor is not null)
            await _processor.StopProcessingAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}

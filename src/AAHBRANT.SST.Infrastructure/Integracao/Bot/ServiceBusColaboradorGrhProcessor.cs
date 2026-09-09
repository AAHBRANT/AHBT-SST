using System.Text.Json;
using AAHBRANT.SST.Application.Trabalhadores.Commands;
using Azure.Messaging.ServiceBus;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Integracao.Bot;

// Consumidor da fila de eventos de Colaborador publicados pelo G-RH (Integração G-RH — G-RH é fonte
// única para o cadastro básico, ver SincronizarColaboradorGrhCommand). Contrato de fio: o corpo da
// mensagem é o JSON do próprio SincronizarColaboradorGrhCommand — os nomes de propriedade batem 1:1 e
// o record é desserializado direto pelo construtor posicional (suporte nativo do
// System.Text.Json desde o .NET 5). Mesmo padrão de competing-consumers/retry de
// ServiceBusNotificacaoTeamsProcessor.
public class ServiceBusColaboradorGrhProcessor : BackgroundService
{
    private readonly ServiceBusClient _cliente;
    private readonly ServiceBusOptions _opcoes;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ServiceBusColaboradorGrhProcessor> _logger;
    private ServiceBusProcessor? _processor;

    public ServiceBusColaboradorGrhProcessor(
        ServiceBusClient cliente,
        IOptions<ServiceBusOptions> opcoes,
        IServiceScopeFactory scopeFactory,
        ILogger<ServiceBusColaboradorGrhProcessor> logger)
    {
        _cliente = cliente;
        _opcoes = opcoes.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor = _cliente.CreateProcessor(_opcoes.FilaColaboradorGrh, new ServiceBusProcessorOptions());
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
        SincronizarColaboradorGrhCommand? comando;
        try
        {
            comando = JsonSerializer.Deserialize<SincronizarColaboradorGrhCommand>(args.Message.Body.ToString());
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Payload inválido na fila de colaboradores do G-RH.");
            await args.DeadLetterMessageAsync(args.Message, "corpo-invalido", cancellationToken: args.CancellationToken);
            return;
        }

        if (comando is null)
        {
            await args.DeadLetterMessageAsync(args.Message, "corpo-invalido", cancellationToken: args.CancellationToken);
            return;
        }

        using var escopo = _scopeFactory.CreateScope();
        var mediator = escopo.ServiceProvider.GetRequiredService<IMediator>();
        var numeroTentativa = args.Message.DeliveryCount;

        try
        {
            await mediator.Send(comando, args.CancellationToken);
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Falha ao sincronizar colaborador do G-RH (CPF hash oculto, tentativa {Tentativa}).",
                numeroTentativa);
            await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken);
        }
    }

    private Task ProcessarErroAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception, "Erro no processor do Service Bus (fila de colaboradores do G-RH).");
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor is not null)
            await _processor.StopProcessingAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}

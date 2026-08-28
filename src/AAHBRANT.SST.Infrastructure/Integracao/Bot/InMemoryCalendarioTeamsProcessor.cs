using AAHBRANT.SST.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Infrastructure.Integracao.Bot;

// Consumidor do fallback em memória (ver InMemoryFilaCalendarioTeams) — mesmo padrão de
// InMemoryNotificacaoTeamsProcessor, com retry manual (backoff simples, 3 tentativas). A lógica de
// criar/atualizar/cancelar e o rastreio de estado em CalendarioEventoTeams ficam em
// CalendarioTeamsMensagemHandler, compartilhada com o consumidor real do Service Bus.
public class InMemoryCalendarioTeamsProcessor : BackgroundService
{
    private const int MaxTentativas = 3;

    private readonly InMemoryFilaCalendarioTeams _fila;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InMemoryCalendarioTeamsProcessor> _logger;

    public InMemoryCalendarioTeamsProcessor(
        InMemoryFilaCalendarioTeams fila, IServiceScopeFactory scopeFactory, ILogger<InMemoryCalendarioTeamsProcessor> logger)
    {
        _fila = fila;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var mensagem in _fila.Reader.ReadAllAsync(stoppingToken))
            {
                await ProcessarComRetryAsync(mensagem, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // encerramento normal do host
        }
    }

    private async Task ProcessarComRetryAsync(CalendarioTeamsMensagem mensagem, CancellationToken ct)
    {
        for (var tentativa = 1; tentativa <= MaxTentativas; tentativa++)
        {
            using var escopo = _scopeFactory.CreateScope();
            var db = escopo.ServiceProvider.GetRequiredService<IAppDbContext>();
            var calendario = escopo.ServiceProvider.GetRequiredService<ICalendarioTeamsService>();

            try
            {
                await CalendarioTeamsMensagemHandler.ProcessarAsync(mensagem, db, calendario, ct);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Falha ao sincronizar calendário Teams para a origem {Tipo}/{Id} (tentativa {Tentativa}/{Max}).",
                    mensagem.EntidadeOrigemTipo, mensagem.EntidadeOrigemId, tentativa, MaxTentativas);

                if (tentativa < MaxTentativas)
                    await Task.Delay(TimeSpan.FromSeconds(5 * tentativa), ct);
            }
        }
    }
}

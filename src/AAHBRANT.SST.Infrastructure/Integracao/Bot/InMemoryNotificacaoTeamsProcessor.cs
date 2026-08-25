using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Infrastructure.Integracao.Bot;

// Consumidor do fallback em memória (ver InMemoryFilaNotificacaoTeams) — lê cada
// NotificacaoTeamsMensagem enfileirada e tenta enviar via INotificacaoTeamsService, com retry manual
// (backoff simples) e um registro em AlertaHistoricoEnvio por tentativa (canal "Bot"), exatamente
// como o consumidor real do Service Bus faria (ver ServiceBusNotificacaoTeamsProcessor). Uma falha
// aqui nunca derruba o worker nem a Api — é só logada e tentada de novo até MaxTentativas.
public class InMemoryNotificacaoTeamsProcessor : BackgroundService
{
    private const int MaxTentativas = 3;

    private readonly InMemoryFilaNotificacaoTeams _fila;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InMemoryNotificacaoTeamsProcessor> _logger;

    public InMemoryNotificacaoTeamsProcessor(
        InMemoryFilaNotificacaoTeams fila, IServiceScopeFactory scopeFactory, ILogger<InMemoryNotificacaoTeamsProcessor> logger)
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

    private async Task ProcessarComRetryAsync(NotificacaoTeamsMensagem mensagem, CancellationToken ct)
    {
        for (var tentativa = 1; tentativa <= MaxTentativas; tentativa++)
        {
            using var escopo = _scopeFactory.CreateScope();
            var db = escopo.ServiceProvider.GetRequiredService<IAppDbContext>();
            var notificacao = escopo.ServiceProvider.GetRequiredService<INotificacaoTeamsService>();

            try
            {
                await notificacao.EnviarAsync(mensagem.DestinatarioUsuarioId, mensagem.Titulo, mensagem.Descricao, ct);

                db.AlertaHistoricoEnvios.Add(new AlertaHistoricoEnvio
                {
                    AlertaId = mensagem.AlertaId,
                    Canal = "ActivityFeed",
                    Sucesso = true,
                    NumeroTentativa = tentativa,
                });
                await db.SaveChangesAsync(ct);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Falha ao enviar notificação Teams para o usuário {UsuarioId} (tentativa {Tentativa}/{Max}).",
                    mensagem.DestinatarioUsuarioId, tentativa, MaxTentativas);

                db.AlertaHistoricoEnvios.Add(new AlertaHistoricoEnvio
                {
                    AlertaId = mensagem.AlertaId,
                    Canal = "ActivityFeed",
                    Sucesso = false,
                    NumeroTentativa = tentativa,
                    MensagemErro = ex.Message,
                });
                await db.SaveChangesAsync(ct);

                if (tentativa < MaxTentativas)
                    await Task.Delay(TimeSpan.FromSeconds(5 * tentativa), ct);
            }
        }
    }
}

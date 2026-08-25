using System.Threading.Channels;
using AAHBRANT.SST.Application.Common.Interfaces;

namespace AAHBRANT.SST.Infrastructure.Integracao.Bot;

// Fallback local de IFilaNotificacaoTeams, usado enquanto "ServiceBus:ConnectionString" não estiver
// configurada (dev local, CI) — ver AddInfrastructure. Cumpre o mesmo contrato de PROJECT RULES.md
// §4 (enfileira sem bloquear, reprocessa com retry) dentro do processo via Channel<T>; o consumidor é
// InMemoryNotificacaoTeamsProcessor (BackgroundService), registrado junto.
//
// Trade-off aceito deliberadamente: não sobrevive a um restart do processo (mensagens em trânsito se
// perdem). Aceitável para desenvolvimento local/CI; troca para Azure.Messaging.ServiceBus (persistente,
// com dead-letter) assim que a connection string real existir em config — nenhum outro código muda,
// só a implementação de IFilaNotificacaoTeams resolvida pela DI.
public class InMemoryFilaNotificacaoTeams : IFilaNotificacaoTeams
{
    private readonly Channel<NotificacaoTeamsMensagem> _canal = Channel.CreateUnbounded<NotificacaoTeamsMensagem>();

    public ChannelReader<NotificacaoTeamsMensagem> Reader => _canal.Reader;

    public Task EnfileirarAsync(NotificacaoTeamsMensagem mensagem, CancellationToken ct = default)
        => _canal.Writer.WriteAsync(mensagem, ct).AsTask();
}

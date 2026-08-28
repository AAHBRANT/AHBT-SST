using System.Threading.Channels;
using AAHBRANT.SST.Application.Common.Interfaces;

namespace AAHBRANT.SST.Infrastructure.Integracao.Bot;

// Fallback local de IFilaCalendarioTeams, usado enquanto "ServiceBus:ConnectionString" não estiver
// configurada (dev local, CI) — mesmo padrão de InMemoryFilaNotificacaoTeams. O consumidor é
// InMemoryCalendarioTeamsProcessor (BackgroundService), registrado junto.
public class InMemoryFilaCalendarioTeams : IFilaCalendarioTeams
{
    private readonly Channel<CalendarioTeamsMensagem> _canal = Channel.CreateUnbounded<CalendarioTeamsMensagem>();

    public ChannelReader<CalendarioTeamsMensagem> Reader => _canal.Reader;

    public Task EnfileirarAsync(CalendarioTeamsMensagem mensagem, CancellationToken ct = default)
        => _canal.Writer.WriteAsync(mensagem, ct).AsTask();
}

using System.Text.Json;
using AAHBRANT.SST.Application.Common.Interfaces;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Integracao.Bot;

// Implementação real de IFilaCalendarioTeams — mesmo padrão de ServiceBusFilaNotificacaoTeams. Só é
// registrada em DI quando "ServiceBus:ConnectionString" existir em config (ver AddInfrastructure); o
// namespace e a fila (nome em "ServiceBus:FilaCalendarioTeams") precisam ser provisionados manualmente
// no Azure antes disso funcionar de verdade — até lá, o fallback em memória assume o lugar.
public class ServiceBusFilaCalendarioTeams : IFilaCalendarioTeams, IAsyncDisposable
{
    private readonly ServiceBusSender _sender;

    public ServiceBusFilaCalendarioTeams(ServiceBusClient cliente, IOptions<ServiceBusOptions> opcoes)
    {
        _sender = cliente.CreateSender(opcoes.Value.FilaCalendarioTeams);
    }

    public async Task EnfileirarAsync(CalendarioTeamsMensagem mensagem, CancellationToken ct = default)
    {
        var corpo = JsonSerializer.Serialize(mensagem);
        await _sender.SendMessageAsync(new ServiceBusMessage(corpo), ct);
    }

    public ValueTask DisposeAsync() => _sender.DisposeAsync();
}

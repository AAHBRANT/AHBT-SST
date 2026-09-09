using System.Text.Json;
using AAHBRANT.SST.Application.Common.Interfaces;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Integracao.Bot;

// Implementação real de IPublicadorAcidenteGrh. Só é registrada em DI quando
// "ServiceBus:ConnectionString" existir em config (ver AddInfrastructure) — a fila
// ("ServiceBus:FilaAcidenteGrh") é provisionada manualmente no Azure e consumida por um processo do
// próprio G-RH, fora deste repositório.
public class ServiceBusPublicadorAcidenteGrh : IPublicadorAcidenteGrh, IAsyncDisposable
{
    private readonly ServiceBusSender _sender;

    public ServiceBusPublicadorAcidenteGrh(ServiceBusClient cliente, IOptions<ServiceBusOptions> opcoes)
    {
        _sender = cliente.CreateSender(opcoes.Value.FilaAcidenteGrh);
    }

    public async Task PublicarAsync(AcidenteGrhEvento evento, CancellationToken ct = default)
    {
        var corpo = JsonSerializer.Serialize(evento);
        await _sender.SendMessageAsync(new ServiceBusMessage(corpo), ct);
    }

    public ValueTask DisposeAsync() => _sender.DisposeAsync();
}

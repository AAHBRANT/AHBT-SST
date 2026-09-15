using AAHBRANT.SST.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Infrastructure.Integracao.Bot;

// Fallback de IPublicadorAcidenteGrh enquanto "ServiceBus:ConnectionString" não estiver configurada
// (dev local, CI) — ver AddInfrastructure. Diferente de InMemoryFilaNotificacaoTeams, não há
// consumidor local para simular aqui (quem consome o evento de Acidente é o processo do G-RH, externo
// a este repositório); só loga, para não travar o registro do acidente nem fingir uma entrega que não
// aconteceu.
public class NoOpPublicadorAcidenteGrh : IPublicadorAcidenteGrh
{
    private readonly ILogger<NoOpPublicadorAcidenteGrh> _logger;

    public NoOpPublicadorAcidenteGrh(ILogger<NoOpPublicadorAcidenteGrh> logger) => _logger = logger;

    public Task PublicarAsync(AcidenteGrhEvento evento, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Service Bus não configurado — evento do acidente {AcidenteId} não foi publicado para o G-RH.",
            evento.AcidenteId);
        return Task.CompletedTask;
    }
}

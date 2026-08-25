using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Infrastructure.Integracao.Bot;

namespace AAHBRANT.SST.Api.IntegrationTests.Bot;

// Cobre o fallback local de IFilaNotificacaoTeams (usado enquanto "ServiceBus:ConnectionString" não
// existir — ver AddInfrastructure). Não testa o Azure Service Bus real nem o envio de fato via Bot
// Framework (fora do escopo verificável neste ambiente); só garante que uma mensagem enfileirada fica
// disponível para o consumidor (InMemoryNotificacaoTeamsProcessor) ler, na ordem em que foi enviada.
public class InMemoryFilaNotificacaoTeamsTests
{
    [Fact]
    public async Task EnfileirarAsync_DevePermitirLerAMensagemDeVolta()
    {
        var fila = new InMemoryFilaNotificacaoTeams();
        var mensagem = new NotificacaoTeamsMensagem(Guid.NewGuid(), Guid.NewGuid(), "Alerta de teste", "Descrição de teste");

        await fila.EnfileirarAsync(mensagem);

        var lida = await fila.Reader.ReadAsync();

        Assert.Equal(mensagem, lida);
    }

    [Fact]
    public async Task EnfileirarAsync_DeveManterOrdemFifo()
    {
        var fila = new InMemoryFilaNotificacaoTeams();
        var primeira = new NotificacaoTeamsMensagem(Guid.NewGuid(), Guid.NewGuid(), "Primeira", null);
        var segunda = new NotificacaoTeamsMensagem(Guid.NewGuid(), Guid.NewGuid(), "Segunda", null);

        await fila.EnfileirarAsync(primeira);
        await fila.EnfileirarAsync(segunda);

        Assert.Equal(primeira, await fila.Reader.ReadAsync());
        Assert.Equal(segunda, await fila.Reader.ReadAsync());
    }
}

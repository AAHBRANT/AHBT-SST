using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Integracao.Bot;

namespace AAHBRANT.SST.Api.IntegrationTests.Bot;

// Cobre o fallback local de IFilaCalendarioTeams (usado enquanto "ServiceBus:ConnectionString" não
// existir — ver AddInfrastructure), mesmo padrão de InMemoryFilaNotificacaoTeamsTests. Não testa o
// Azure Service Bus real nem a chamada de fato ao Microsoft Graph (fora do escopo verificável neste
// ambiente); só garante que uma mensagem enfileirada fica disponível para o consumidor
// (InMemoryCalendarioTeamsProcessor) ler, na ordem em que foi enviada.
public class InMemoryFilaCalendarioTeamsTests
{
    [Fact]
    public async Task EnfileirarAsync_DevePermitirLerAMensagemDeVolta()
    {
        var fila = new InMemoryFilaCalendarioTeams();
        var mensagem = new CalendarioTeamsMensagem(
            "Alerta", Guid.NewGuid(), OperacaoCalendarioTeams.Criar, Guid.NewGuid(),
            "Título de teste", "Descrição de teste", DateTime.UtcNow.Date);

        await fila.EnfileirarAsync(mensagem);

        var lida = await fila.Reader.ReadAsync();

        Assert.Equal(mensagem, lida);
    }

    [Fact]
    public async Task EnfileirarAsync_DeveManterOrdemFifo()
    {
        var fila = new InMemoryFilaCalendarioTeams();
        var primeira = new CalendarioTeamsMensagem(
            "Alerta", Guid.NewGuid(), OperacaoCalendarioTeams.Criar, Guid.NewGuid(), "Primeira", null, DateTime.UtcNow.Date);
        var segunda = new CalendarioTeamsMensagem(
            "Alerta", Guid.NewGuid(), OperacaoCalendarioTeams.Cancelar, Guid.NewGuid(), null, null, null);

        await fila.EnfileirarAsync(primeira);
        await fila.EnfileirarAsync(segunda);

        Assert.Equal(primeira, await fila.Reader.ReadAsync());
        Assert.Equal(segunda, await fila.Reader.ReadAsync());
    }
}

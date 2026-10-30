using System.Net;
using AAHBRANT.SST.Application.Calendario;
using AAHBRANT.SST.Infrastructure.Integracao.Teams;

namespace AAHBRANT.SST.Application.Tests.Calendario;

public class CalendarioGraphErrosTests
{
    [Fact]
    public async Task BloqueioDoExchangeExplicaPermissaoSemExporRespostaTecnica()
    {
        using var resposta = new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("{\"error\":{\"code\":\"ErrorAccessDenied\",\"message\":\"Access to OData is disabled: [RAOP] : Blocked by tenant configured AppOnly AccessPolicy settings.\"}}")
        };
        var erro = await Assert.ThrowsAsync<CalendarioTeamsIndisponivelException>(() =>
            GraphCalendarioTeamsService.GarantirSucessoAsync(resposta, Guid.NewGuid(), default));
        Assert.Contains("escopo de acesso", erro.Message);
        Assert.DoesNotContain("OData", erro.Message);
        Assert.Contains("RAOP", erro.InnerException!.Message);
    }

    [Fact]
    public void IntervaloDaConsultaMantemUtcExplicito()
    {
        Assert.Equal("2026-10-29T03:00:00.0000000Z", GraphCalendarioTeamsService.FormatarLimiteConsulta(new(2026, 10, 29, 3, 0, 0, DateTimeKind.Utc)));
    }
}

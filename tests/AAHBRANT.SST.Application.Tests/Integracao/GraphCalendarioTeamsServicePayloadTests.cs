using AAHBRANT.SST.Infrastructure.Integracao.Teams;

namespace AAHBRANT.SST.Application.Tests.Integracao;

// GraphCalendarioTeamsService.CriarEventoAsync/AtualizarEventoAsync/CancelarEventoAsync não são
// testáveis por aqui sem um refactor maior: PrepararRequisicaoAsync constrói um
// ClientSecretCredential real e chama GetTokenAsync — uma chamada de rede de verdade ao Azure AD, sem
// nenhum seam de injeção (TokenCredential/HttpMessageHandler) para substituir por um fake. Em vez de
// forçar isso fora do escopo pedido, este teste cobre só a lógica pura que dá pra isolar: o cálculo de
// fronteira de dia e o formato do payload enviado ao Graph (MontarEventoDiaInteiro, tornado internal +
// InternalsVisibleTo só para isso — ver AAHBRANT.SST.Infrastructure/AssemblyInfo.cs). O retorno é
// object (tipo anônimo) porque é isso que o Graph espera serializado; daí o uso de dynamic aqui.
public class GraphCalendarioTeamsServicePayloadTests
{
    [Fact]
    public void MontarEventoDiaInteiro_CalculaFimComoInicioMaisUmDia()
    {
        var data = new DateTime(2026, 8, 28, 15, 30, 0); // horário no meio do dia — deve ser ignorado (Date).

        dynamic evento = GraphCalendarioTeamsService.MontarEventoDiaInteiro("Título", "Descrição", data);

        Assert.Equal("2026-08-28T00:00:00", (string)evento.start.dateTime);
        Assert.Equal("2026-08-29T00:00:00", (string)evento.end.dateTime);
    }

    [Fact]
    public void MontarEventoDiaInteiro_VirandoOAno_AvancaParaOAnoSeguinteCorretamente()
    {
        var data = new DateTime(2026, 12, 31);

        dynamic evento = GraphCalendarioTeamsService.MontarEventoDiaInteiro("Título", null, data);

        Assert.Equal("2026-12-31T00:00:00", (string)evento.start.dateTime);
        Assert.Equal("2027-01-01T00:00:00", (string)evento.end.dateTime);
    }

    [Fact]
    public void MontarEventoDiaInteiro_PreencheCamposFixosDeEventoDeDiaInteiro()
    {
        dynamic evento = GraphCalendarioTeamsService.MontarEventoDiaInteiro("Título", "Descrição", DateTime.UtcNow.Date);

        Assert.True((bool)evento.isAllDay);
        Assert.Equal("free", (string)evento.showAs);
        Assert.Equal("America/Sao_Paulo", (string)evento.start.timeZone);
        Assert.Equal("America/Sao_Paulo", (string)evento.end.timeZone);
        Assert.Equal("text", (string)evento.body.contentType);
    }

    [Fact]
    public void MontarEventoDiaInteiro_ComTituloEDescricao_PropagaParaOPayload()
    {
        dynamic evento = GraphCalendarioTeamsService.MontarEventoDiaInteiro("ASO vencendo", "Detalhe do alerta", DateTime.UtcNow.Date);

        Assert.Equal("ASO vencendo", (string)evento.subject);
        Assert.Equal("Detalhe do alerta", (string)evento.body.content);
    }

    [Fact]
    public void MontarEventoDiaInteiro_SemDescricao_UsaStringVaziaEmVezDeNulo()
    {
        dynamic evento = GraphCalendarioTeamsService.MontarEventoDiaInteiro("Título", null, DateTime.UtcNow.Date);

        Assert.Equal(string.Empty, (string)evento.body.content);
    }
}

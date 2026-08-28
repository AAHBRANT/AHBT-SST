using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Domain.Interfaces;

namespace AAHBRANT.SST.Application.Tests.Alertas;

// Test doubles escritos à mão (repositório não usa mocking library nenhuma — ver
// tests/**/*.csproj) compartilhados pelos testes de CalendarioTeamsMensagemHandler,
// AlertaEngineService e do ciclo CriarAlertaCommand/ResolverAlertaCommand.

public class CalendarioTeamsServiceFalso : ICalendarioTeamsService
{
    public List<(Guid OrganizadorUsuarioId, string Titulo, string? Descricao, DateTime Data)> EventosCriados { get; } = new();
    public List<(Guid OrganizadorUsuarioId, string GraphEventId, string Titulo, string? Descricao, DateTime Data)> EventosAtualizados { get; } = new();
    public List<(Guid OrganizadorUsuarioId, string GraphEventId)> EventosCancelados { get; } = new();

    public string? GraphEventIdARetornar { get; set; } = Guid.NewGuid().ToString();
    public Exception? ExcecaoAoCriar { get; set; }

    public Task<string> CriarEventoAsync(
        Guid organizadorUsuarioId, string titulo, string? descricao, DateTime data, CancellationToken ct = default)
    {
        if (ExcecaoAoCriar is not null) throw ExcecaoAoCriar;
        EventosCriados.Add((organizadorUsuarioId, titulo, descricao, data));
        return Task.FromResult(GraphEventIdARetornar!);
    }

    public Task AtualizarEventoAsync(
        Guid organizadorUsuarioId, string graphEventId, string titulo, string? descricao, DateTime data,
        CancellationToken ct = default)
    {
        EventosAtualizados.Add((organizadorUsuarioId, graphEventId, titulo, descricao, data));
        return Task.CompletedTask;
    }

    public Task CancelarEventoAsync(Guid organizadorUsuarioId, string graphEventId, CancellationToken ct = default)
    {
        EventosCancelados.Add((organizadorUsuarioId, graphEventId));
        return Task.CompletedTask;
    }
}

public class FilaCalendarioTeamsFalsa : IFilaCalendarioTeams
{
    public List<CalendarioTeamsMensagem> Mensagens { get; } = new();

    public Task EnfileirarAsync(CalendarioTeamsMensagem mensagem, CancellationToken ct = default)
    {
        Mensagens.Add(mensagem);
        return Task.CompletedTask;
    }
}

public class FilaNotificacaoTeamsFalsa : IFilaNotificacaoTeams
{
    public List<NotificacaoTeamsMensagem> Mensagens { get; } = new();

    public Task EnfileirarAsync(NotificacaoTeamsMensagem mensagem, CancellationToken ct = default)
    {
        Mensagens.Add(mensagem);
        return Task.CompletedTask;
    }
}

public class AlertaOrigemProviderFalso : IAlertaOrigemProvider
{
    public TipoModuloAlerta Modulo { get; set; } = TipoModuloAlerta.Aso;
    public List<AlertaOrigemItem> Itens { get; set; } = new();

    public Task<List<AlertaOrigemItem>> ObterItensAsync(CancellationToken ct = default) => Task.FromResult(Itens);
}

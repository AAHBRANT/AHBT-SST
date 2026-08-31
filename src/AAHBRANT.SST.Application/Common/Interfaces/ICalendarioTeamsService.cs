using AAHBRANT.SST.Application.Calendario;

namespace AAHBRANT.SST.Application.Common.Interfaces;

// Fala com o Microsoft Graph (/users/{aadObjectId}/events) para sincronizar vencimentos do Motor de
// Alertas com o Calendário do Teams/Outlook do destinatário (docs/superpowers/specs/
// 2026-08-28-calendario-teams-design.md). Implementado em Infrastructure (GraphCalendarioTeamsService)
// porque depende do SDK do Graph/Azure.Identity, que a Application não referencia.
//
// Mesmo princípio de INotificacaoTeamsService: lança exceção em vez de engolir a falha — quem chama é
// sempre um consumidor da fila de retry (ver IFilaCalendarioTeams), que decide o que fazer com o erro
// (nova tentativa, gravar em CalendarioEventoTeams.MensagemErro).
public interface ICalendarioTeamsService
{
    Task<string> CriarEventoAsync(
        Guid organizadorUsuarioId, string titulo, string? descricao, DateTime data, CancellationToken ct = default);

    Task AtualizarEventoAsync(
        Guid organizadorUsuarioId, string graphEventId, string titulo, string? descricao, DateTime data,
        CancellationToken ct = default);

    Task CancelarEventoAsync(Guid organizadorUsuarioId, string graphEventId, CancellationToken ct = default);

    // Leitura (requisito do usuário, 2026-08-29: "quero o calendário do Teams dentro do
    // aplicativo") — GET /users/{aadObjectId}/calendarView, mesma permissão de aplicativo
    // (Calendars.ReadWrite já cobre leitura) e mesmo App Registration usado para criar/atualizar/
    // cancelar acima. Consumida por ObterCalendarioQuery.
    Task<IReadOnlyList<EventoGraphDto>> ListarEventosAsync(
        Guid usuarioId, DateTime inicio, DateTime fim, CancellationToken ct = default);
}

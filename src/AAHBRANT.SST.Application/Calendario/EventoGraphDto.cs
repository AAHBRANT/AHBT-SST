namespace AAHBRANT.SST.Application.Calendario;

// Evento real do Calendário do Outlook/Teams do usuário, lido via Microsoft Graph
// (GET /users/{aadObjectId}/calendarView) — GraphCalendarioTeamsService.ListarEventosAsync.
// Campos deliberadamente simples/planos (sem espelhar 1:1 o JSON do Graph): só o que a tela de
// calendário do app precisa mostrar.
public record EventoGraphDto(
    string GraphEventId,
    string Assunto,
    DateTime Inicio,
    DateTime Fim,
    bool DiaInteiro,
    string? Local,
    string? OrganizadorNome,
    bool ReuniaoOnline,
    string? LinkReuniaoOnline);

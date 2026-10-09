namespace AAHBRANT.SST.Application.Common.Interfaces;

/// <summary>
/// Reunião do Teams (com horário, convidados e link) na agenda do organizador — diferente dos
/// lembretes de dia inteiro do Motor de Alertas (ICalendarioTeamsService). Usada pela reunião de
/// análise de acidente (09/10/2026).
/// </summary>
public interface IReuniaoTeamsService
{
    /// <summary>Falso quando o Graph não está configurado (ex.: ambiente local).</summary>
    bool Configurado { get; }

    /// <param name="inicio">Horário de Brasília.</param>
    Task<ReuniaoTeamsCriada> CriarReuniaoAsync(
        Guid organizadorUsuarioId,
        string titulo,
        string descricao,
        DateTime inicio,
        DateTime fim,
        IReadOnlyList<string> emailsConvidados,
        CancellationToken ct = default);
}

public record ReuniaoTeamsCriada(string GraphEventId, string? LinkTeams);

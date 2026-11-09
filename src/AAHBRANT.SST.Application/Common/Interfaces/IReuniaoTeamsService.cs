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
        CancellationToken ct = default,
        RecorrenciaReuniao? recorrencia = null,
        OpcoesCriacaoReuniao? opcoes = null);

    /// <summary>Muda só o horário da reunião já criada; o Teams avisa os convidados.</summary>
    /// <param name="inicio">Horário de Brasília.</param>
    Task RemarcarReuniaoAsync(
        Guid organizadorUsuarioId,
        string graphEventId,
        DateTime inicio,
        DateTime fim,
        CancellationToken ct = default);
}

/// <param name="NaAgendaDoOrganizador">
/// true = criada na agenda de quem pediu, em nome dele (ele é o organizador e adia/cancela pelo próprio
/// Outlook); false = criada na caixa compartilhada (Graph:CaixaReunioes).
/// </param>
public record ReuniaoTeamsCriada(string GraphEventId, string? LinkTeams, bool NaAgendaDoOrganizador = false);

/// <param name="TokenUsuario">
/// Token de acesso de quem está logado (o mesmo recebido pela API). Quando informado, a reunião é criada
/// em nome dele (fluxo On-Behalf-Of, permissão delegada Calendars.ReadWrite) e ele vira o organizador.
/// Sem a permissão concedida no Entra ID, cai na caixa compartilhada.
/// </param>
/// <param name="EmailOrganizador">Convidado quando a reunião cai na caixa compartilhada, para que receba o convite.</param>
public record OpcoesCriacaoReuniao(string? TokenUsuario, string? EmailOrganizador);

// Repetição da reunião criada pelo Calendário (09/11/2026: "faltou a opção de repetir toda semana").
// Semanal/Quinzenal repetem no mesmo dia da semana do início; Mensal no mesmo dia do mês.
public enum TipoRecorrenciaReuniao
{
    Semanal = 1,
    Quinzenal = 2,
    Mensal = 3,
}

/// <param name="Ate">Última data em que a reunião pode acontecer (inclusive).</param>
public record RecorrenciaReuniao(TipoRecorrenciaReuniao Tipo, DateOnly Ate);

using AAHBRANT.SST.Application.Alertas.Motor;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.SuporteIa;

// Evento no calendário do Teams para cada chamado do Suporte IA (pedido do usuário, 24/09/2026). O
// evento pertence ao Alerta do chamado (mesma origem "Alerta" do Motor de Alertas), então resolver,
// ignorar ou excluir o alerta pela tela de Alertas também tira o evento do calendário.
public static class SuporteIaCalendario
{
    private static readonly TimeZoneInfo FusoBrasilia = ObterFusoBrasilia();

    // Prazo de atendimento por severidade, em dias úteis (sábado e domingo não contam; feriados não
    // são considerados): Crítica no mesmo dia, Alta 1, Média 3, Baixa 5.
    public static DateTime CalcularPrazo(SeveridadeSolicitacaoSuporteIa severidade, DateTime agoraUtc)
    {
        var diasUteis = severidade switch
        {
            SeveridadeSolicitacaoSuporteIa.Critica => 0,
            SeveridadeSolicitacaoSuporteIa.Alta => 1,
            SeveridadeSolicitacaoSuporteIa.Media => 3,
            _ => 5,
        };

        var data = TimeZoneInfo.ConvertTimeFromUtc(agoraUtc, FusoBrasilia).Date;
        while (diasUteis > 0)
        {
            data = data.AddDays(1);
            if (data.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                diasUteis--;
        }
        return data;
    }

    // Chamado encerrado (resolvido ou recusado): fecha o alerta ainda aberto e cancela o evento, se
    // ele chegou a ser criado no calendário.
    public static async Task EncerrarAsync(
        IAppDbContext db, IFilaCalendarioTeams filaCalendario, SuporteIaSolicitacao solicitacao, CancellationToken ct)
    {
        if (!solicitacao.AlertaId.HasValue) return;

        var alerta = await db.Alertas.FirstOrDefaultAsync(a => a.Id == solicitacao.AlertaId.Value, ct);
        if (alerta is null) return;

        if (alerta.Status is not (StatusAlerta.Resolvido or StatusAlerta.Ignorado))
            alerta.Status = StatusAlerta.Resolvido;
        await db.SaveChangesAsync(ct);

        if (!alerta.DestinatarioUsuarioId.HasValue) return;

        var existeEventoCriado = await db.CalendariosEventosTeams.AnyAsync(
            c => c.EntidadeOrigemTipo == AlertaEngineService.OrigemCalendarioAlerta
                && c.EntidadeOrigemId == alerta.Id
                && c.Status == StatusCalendarioEvento.Criado,
            ct);
        if (!existeEventoCriado) return;

        try
        {
            await filaCalendario.EnfileirarAsync(
                new CalendarioTeamsMensagem(
                    AlertaEngineService.OrigemCalendarioAlerta, alerta.Id, OperacaoCalendarioTeams.Cancelar,
                    alerta.DestinatarioUsuarioId.Value, null, null, null),
                ct);
        }
        catch
        {
            // O encerramento do chamado já foi gravado; falha na fila não deve derrubar a resposta.
        }
    }

    private static TimeZoneInfo ObterFusoBrasilia()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"); }
    }
}

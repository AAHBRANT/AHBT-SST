using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Infrastructure.Integracao.Bot;

// Lógica de processamento de CalendarioTeamsMensagem compartilhada pelos dois consumidores
// (ServiceBusCalendarioTeamsProcessor / InMemoryCalendarioTeamsProcessor) — extraída em vez de
// duplicada porque, ao contrário dos processadores de notificação, aqui há estado a rastrear
// (CalendarioEventoTeams.GraphEventId/Status) e divergir essa regra entre as duas implementações
// criaria um bug fácil de introduzir sem perceber.
//
// Ver docs/superpowers/specs/2026-08-28-calendario-teams-design.md §4.3/§5 para o desenho completo,
// incluindo o cenário de borda: Atualizar/Cancelar chegando para uma origem cujo evento nunca foi
// criado com sucesso (Status != Criado) — não há o que fazer no Graph, a mensagem é descartada.
internal static class CalendarioTeamsMensagemHandler
{
    public static async Task ProcessarAsync(
        CalendarioTeamsMensagem mensagem, IAppDbContext db, ICalendarioTeamsService calendario, CancellationToken ct)
    {
        var registro = await db.CalendariosEventosTeams.FirstOrDefaultAsync(
            c => c.EntidadeOrigemTipo == mensagem.EntidadeOrigemTipo && c.EntidadeOrigemId == mensagem.EntidadeOrigemId, ct);

        if (registro is null)
        {
            if (mensagem.Operacao != OperacaoCalendarioTeams.Criar)
                return;

            registro = new CalendarioEventoTeams
            {
                EntidadeOrigemTipo = mensagem.EntidadeOrigemTipo,
                EntidadeOrigemId = mensagem.EntidadeOrigemId,
                OrganizadorUsuarioId = mensagem.OrganizadorUsuarioId,
            };
            db.CalendariosEventosTeams.Add(registro);
        }

        try
        {
            switch (mensagem.Operacao)
            {
                case OperacaoCalendarioTeams.Criar:
                    if (registro.Status == StatusCalendarioEvento.Criado && !string.IsNullOrWhiteSpace(registro.GraphEventId))
                    {
                        // Redelivery/duplicata — trata como atualização para não duplicar o evento.
                        await calendario.AtualizarEventoAsync(
                            mensagem.OrganizadorUsuarioId, registro.GraphEventId, mensagem.Titulo ?? string.Empty,
                            mensagem.Descricao, mensagem.Data ?? DateTime.UtcNow.Date, ct);
                    }
                    else
                    {
                        registro.GraphEventId = await calendario.CriarEventoAsync(
                            mensagem.OrganizadorUsuarioId, mensagem.Titulo ?? string.Empty, mensagem.Descricao,
                            mensagem.Data ?? DateTime.UtcNow.Date, ct);
                    }
                    registro.Status = StatusCalendarioEvento.Criado;
                    registro.MensagemErro = null;
                    break;

                case OperacaoCalendarioTeams.Atualizar:
                    if (registro.Status != StatusCalendarioEvento.Criado || string.IsNullOrWhiteSpace(registro.GraphEventId))
                        return;

                    await calendario.AtualizarEventoAsync(
                        mensagem.OrganizadorUsuarioId, registro.GraphEventId, mensagem.Titulo ?? string.Empty,
                        mensagem.Descricao, mensagem.Data ?? DateTime.UtcNow.Date, ct);
                    registro.MensagemErro = null;
                    break;

                case OperacaoCalendarioTeams.Cancelar:
                    if (registro.Status != StatusCalendarioEvento.Criado || string.IsNullOrWhiteSpace(registro.GraphEventId))
                        return;

                    await calendario.CancelarEventoAsync(mensagem.OrganizadorUsuarioId, registro.GraphEventId, ct);
                    registro.Status = StatusCalendarioEvento.Cancelado;
                    registro.MensagemErro = null;
                    break;
            }

            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            registro.Status = StatusCalendarioEvento.Falhou;
            registro.MensagemErro = ex.Message;
            await db.SaveChangesAsync(ct);
            throw;
        }
    }
}

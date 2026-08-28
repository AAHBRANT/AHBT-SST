using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Common.Interfaces;

// Fila dedicada de calendário (docs/superpowers/specs/2026-08-28-calendario-teams-design.md),
// espelhando exatamente o padrão de IFilaNotificacaoTeams: quem cria/atualiza/resolve um Alerta com
// destinatário só chama EnfileirarAsync e segue em frente — a chamada de fato ao Microsoft Graph (e o
// retry em caso de falha) acontece em background, fora do fluxo que mutou o Alerta.
//
// Duas implementações em Infrastructure, escolhidas em tempo de DI (AddInfrastructure) conforme
// "ServiceBus:ConnectionString" existir ou não — mesmo critério já usado para IFilaNotificacaoTeams.
public interface IFilaCalendarioTeams
{
    Task EnfileirarAsync(CalendarioTeamsMensagem mensagem, CancellationToken ct = default);
}

// Data é irrelevante para Operacao == Cancelar (o evento existente é só removido do calendário).
public record CalendarioTeamsMensagem(
    string EntidadeOrigemTipo,
    Guid EntidadeOrigemId,
    OperacaoCalendarioTeams Operacao,
    Guid OrganizadorUsuarioId,
    string? Titulo,
    string? Descricao,
    DateTime? Data);

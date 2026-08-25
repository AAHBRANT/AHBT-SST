namespace AAHBRANT.SST.Application.Common.Interfaces;

// Abstração exigida por PROJECT RULES.md §4: "falhas no envio de notificações para o Teams devem
// ser registradas em fila no Azure Service Bus e reprocessadas sem travar a aplicação". Quem cria um
// Alerta com DestinatarioUsuarioId preenchido (CriarAlertaCommand, AlertaEngineService) só chama
// EnfileirarAsync e segue em frente — o envio de fato (e o retry em caso de falha) acontece em
// background, fora do fluxo que criou o alerta.
//
// Duas implementações em Infrastructure, escolhidas em tempo de DI (AddInfrastructure) conforme a
// config existir ou não:
//   - ServiceBusFilaNotificacaoTeams: real, usa Azure.Messaging.ServiceBus — ativa quando
//     "ServiceBus:ConnectionString" estiver preenchida (o namespace/fila é provisionado manualmente
//     no Azure depois, fora do escopo desta tarefa).
//   - InMemoryFilaNotificacaoTeams: fallback local (Channel<T> + BackgroundService com retry) usado
//     em desenvolvimento/CI enquanto o Service Bus real não existir, para não travar o ambiente.
public interface IFilaNotificacaoTeams
{
    Task EnfileirarAsync(NotificacaoTeamsMensagem mensagem, CancellationToken ct = default);
}

public record NotificacaoTeamsMensagem(Guid AlertaId, Guid DestinatarioUsuarioId, string Titulo, string? Descricao);

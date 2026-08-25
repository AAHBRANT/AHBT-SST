namespace AAHBRANT.SST.Application.Common.Interfaces;

// Motor Central de Alertas, Etapa 4 — envia a notificação de "sino" do Teams (Activity Feed) para o
// Usuario via Microsoft Graph (POST /users/{aadObjectId}/teamwork/sendActivityNotification), sem Bot
// Framework e sem depender de nenhuma interação anterior do usuário com um bot. Implementado em
// Infrastructure (GraphActivityNotificacaoTeamsService) porque depende do SDK do Graph/Azure.Identity,
// que a Application não referencia (Clean Architecture).
//
// Deliberadamente lança exceção em vez de engolir a falha (usuário sem AzureAdObjectId cadastrado,
// Graph:ClientSecret não configurado etc.) — quem chama este serviço é sempre um consumidor da fila
// de retry (PROJECT RULES.md §4), que decide o que fazer com a falha (nova tentativa, registrar em
// AlertaHistoricoEnvio, dead-letter). Nunca é chamado diretamente pelo fluxo que cria o Alerta.
public interface INotificacaoTeamsService
{
    Task<bool> EnviarAsync(Guid usuarioId, string titulo, string? descricao, CancellationToken ct = default);
}

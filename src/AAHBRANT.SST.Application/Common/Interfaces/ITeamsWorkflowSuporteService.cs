using AAHBRANT.SST.Domain.Entidades;

namespace AAHBRANT.SST.Application.Common.Interfaces;

// Aviso de chamado do Suporte IA no chat do Teams (pedido do usuário, 24/09/2026). O Graph não deixa
// um sistema mandar mensagem de chat sem usuário logado, então o envio passa por um Workflow do
// próprio Teams ("Quando uma solicitação de webhook do Teams for recebida → Postar no chat"), que o
// responsável cria e cuja URL fica em TeamsWorkflow:SuporteWebhookUrl.
public interface ITeamsWorkflowSuporteService
{
    Task EnviarDemandaAsync(SuporteIaSolicitacao solicitacao, CancellationToken ct = default);
}

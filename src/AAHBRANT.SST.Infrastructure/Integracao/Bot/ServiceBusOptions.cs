namespace AAHBRANT.SST.Infrastructure.Integracao.Bot;

// Ver PROJECT RULES.md §4 — fila de retry para falhas de envio de notificação Teams. Enquanto
// "ServiceBus:ConnectionString" estiver vazia, AddInfrastructure registra InMemoryFilaNotificacaoTeams
// no lugar (ver DependencyInjection.cs); o namespace/fila real no Azure é provisionado manualmente,
// fora do escopo desta tarefa.
public class ServiceBusOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string FilaNotificacoesTeams { get; set; } = "notificacoes-teams";
    public string FilaCalendarioTeams { get; set; } = "calendario-teams";
}

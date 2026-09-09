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

    // Integração G-RH (2026-09-09): filas dedicadas, provisionadas manualmente no mesmo namespace do
    // Service Bus acima. "colaborador-grh" é publicada pelo G-RH e consumida aqui
    // (ServiceBusColaboradorGrhProcessor); "acidente-grh" é publicada por este sistema e consumida pelo
    // G-RH (ServiceBusPublicadorAcidenteGrh) — nomes definidos em conjunto com o time do G-RH.
    public string FilaColaboradorGrh { get; set; } = "colaborador-grh";
    public string FilaAcidenteGrh { get; set; } = "acidente-grh";
}

namespace AAHBRANT.SST.Infrastructure.Integracao.Teams;

// Credenciais do App Registration no Entra ID com a permissão de aplicativo TeamsActivity.Send
// (Microsoft Graph) — mesmo padrão de "vazio até o recurso existir" usado em outras integrações.
// Ver appsettings.json / appsettings.Development.json, seção "Graph".
public class GraphOptions
{
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    // Caixa compartilhada que envia os e-mails do SST (Mail.Send restrito a ela por ApplicationAccessPolicy).
    public string RemetenteEmail { get; set; } = "sst@aahbrant.com";
    public string ActivityType { get; set; } = "alertaSst";

    // Caixa (e-mail ou object id) em que as reuniões com convidados são criadas. A Application Access
    // Policy do tenant (grupo SST-Envio-Graph) só libera a caixa do SST para este app; criar a reunião
    // na caixa do organizador devolve 403. Vazio = usa a caixa do próprio organizador (ex.: dev local).
    public string CaixaReunioes { get; set; } = string.Empty;
    public string TopicWebUrl { get; set; } = "https://teams.microsoft.com/l/entity/fdf875ee-359e-4c16-b56e-7ee6e2034dd3/dashboard?webUrl=https%3A%2F%2Fsst-web-hml.kindground-7a44c4f0.brazilsouth.azurecontainerapps.io%2Findex.html%23%2F";
}

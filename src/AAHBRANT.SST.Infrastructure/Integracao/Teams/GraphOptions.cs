namespace AAHBRANT.SST.Infrastructure.Integracao.Teams;

// Credenciais do App Registration no Entra ID com a permissão de aplicativo TeamsActivity.Send
// (Microsoft Graph) — mesmo padrão de "vazio até o recurso existir" usado em outras integrações.
// Ver appsettings.json / appsettings.Development.json, seção "Graph".
public class GraphOptions
{
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string ActivityType { get; set; } = "alertaSst";
    public string TopicWebUrl { get; set; } = "https://teams.microsoft.com/l/entity/fdf875ee-359e-4c16-b56e-7ee6e2034dd3/dashboard?webUrl=https%3A%2F%2Fsst-web-hml.kindground-7a44c4f0.brazilsouth.azurecontainerapps.io%2Findex.html%23%2F";
}

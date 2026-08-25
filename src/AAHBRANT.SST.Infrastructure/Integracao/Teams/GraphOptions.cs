namespace AAHBRANT.SST.Infrastructure.Integracao.Teams;

// Credenciais do App Registration no Entra ID com a permissão de aplicativo TeamsActivity.Send
// (Microsoft Graph) — mesmo padrão de "vazio até o recurso existir" já usado em TelegramOptions.
// Ver appsettings.json / appsettings.Development.json, seção "Graph".
public class GraphOptions
{
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string ActivityType { get; set; } = "alertaSst";
}

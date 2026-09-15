namespace AAHBRANT.SST.Infrastructure.Integracao.Grh;

// Credenciais/endpoint da carga inicial do G-RH (Integração G-RH, contrato acordado em 2026-09-09).
// Mesmo padrão "vazio até o recurso existir" de GraphOptions — ver
// appsettings.json/appsettings.Development.json, seção "Grh". ClientId/ClientSecret são do próprio
// App Registration do SST (o mesmo de AzureAd/Graph); TenantId também é o tenant compartilhado — mas
// ficam duplicados aqui em vez de reaproveitar AzureAdOptions/GraphOptions porque o Scope (recurso do
// G-RH) é específico dessa integração e não faz sentido acoplar as três configurações.
public class GrhOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
}

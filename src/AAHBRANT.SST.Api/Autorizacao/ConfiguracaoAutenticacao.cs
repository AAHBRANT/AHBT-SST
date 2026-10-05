namespace AAHBRANT.SST.Api.Autorizacao;

public static class ConfiguracaoAutenticacao
{
    public static void Validar(IConfiguration configuracao, bool desenvolvimento)
    {
        if (!desenvolvimento && (string.IsNullOrWhiteSpace(configuracao["AzureAd:TenantId"])
            || string.IsNullOrWhiteSpace(configuracao["AzureAd:ClientId"])))
            throw new InvalidOperationException("Configure AzureAd:TenantId e AzureAd:ClientId antes de iniciar a API fora de Development.");
    }
}

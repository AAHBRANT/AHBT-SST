using AAHBRANT.SST.Application.Common.Interfaces;

namespace AAHBRANT.SST.Api.Autorizacao;

// Mesma regra de AssinaturaController.ObterIpCliente: a API roda atrás de reverse proxy no Azure,
// então X-Forwarded-For primeiro; RemoteIpAddress como fallback direto.
public class ClienteIpProvider : IClienteIpProvider
{
    private readonly IHttpContextAccessor _http;

    public ClienteIpProvider(IHttpContextAccessor http) => _http = http;

    public string? ObterIp()
    {
        var contexto = _http.HttpContext;
        if (contexto is null) return null;

        var forwardedFor = contexto.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
            return forwardedFor.Split(',')[0].Trim();

        return contexto.Connection.RemoteIpAddress?.ToString();
    }

    public string? ObterUserAgent() =>
        _http.HttpContext?.Request.Headers.UserAgent.FirstOrDefault();
}

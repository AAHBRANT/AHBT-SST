using AAHBRANT.SST.Application.Common.Interfaces;

namespace AAHBRANT.SST.Api.Autorizacao;

// Mesma regra de AssinaturaController.ObterIpCliente: o IP vem de RemoteIpAddress, que o UseForwardedHeaders
// (Program.cs) já preenche com o IP acrescentado pelo proxy. Não lê X-Forwarded-For na mão: o primeiro valor
// desse cabeçalho é controlado pelo cliente e deixava forjar o IP gravado como evidência (corrigido em 12/10).
public class ClienteIpProvider : IClienteIpProvider
{
    private readonly IHttpContextAccessor _http;

    public ClienteIpProvider(IHttpContextAccessor http) => _http = http;

    public string? ObterIp()
    {
        return _http.HttpContext?.Connection.RemoteIpAddress?.ToString();
    }

    public string? ObterUserAgent() =>
        _http.HttpContext?.Request.Headers.UserAgent.FirstOrDefault();

    public string? ObterAzureAdObjectId()
    {
        var usuario = _http.HttpContext?.User;
        if (usuario?.Identity?.IsAuthenticated != true) return null;
        var oid = usuario.FindFirst("oid")?.Value ?? usuario.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(oid, out _) ? oid : null;
    }
}

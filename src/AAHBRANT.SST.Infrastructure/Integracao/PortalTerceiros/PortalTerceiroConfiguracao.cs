using AAHBRANT.SST.Application.Terceirizados.Portal;
using Microsoft.Extensions.Configuration;

namespace AAHBRANT.SST.Infrastructure.Integracao.PortalTerceiros;

// PortalTerceiros:UrlBase é opcional: o portal é servido pelo mesmo app web do QR de validação
// pública, então na falta dela vale Assinatura:UrlBaseValidacaoPublica — e, sem nenhuma das duas
// (desenvolvimento local), o Vite em localhost.
public class PortalTerceiroConfiguracao : IPortalTerceiroConfiguracao
{
    private readonly IConfiguration _configuration;

    public PortalTerceiroConfiguracao(IConfiguration configuration) => _configuration = configuration;

    public string UrlBase =>
        new[] { _configuration["PortalTerceiros:UrlBase"], _configuration["Assinatura:UrlBaseValidacaoPublica"] }
            .FirstOrDefault(u => !string.IsNullOrWhiteSpace(u))
        ?? "http://localhost:5173";
}

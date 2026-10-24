using AAHBRANT.SST.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace AAHBRANT.SST.Infrastructure.Integracao.SuporteIa;

public class SuporteIaConfiguracao : ISuporteIaConfiguracao
{
    private readonly IConfiguration _configuration;

    public SuporteIaConfiguracao(IConfiguration configuration) => _configuration = configuration;

    public Guid? ResponsavelUsuarioId
        => Guid.TryParse(_configuration["SuporteIa:ResponsavelUsuarioId"], out var id) ? id : null;

    public string? ResponsavelEmail
        => string.IsNullOrWhiteSpace(_configuration["SuporteIa:ResponsavelEmail"])
            ? null
            : _configuration["SuporteIa:ResponsavelEmail"];
}

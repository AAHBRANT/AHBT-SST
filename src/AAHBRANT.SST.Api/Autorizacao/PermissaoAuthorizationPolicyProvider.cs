using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Api.Autorizacao;

// Trata qualquer nome de policy usado em [Authorize(Policy = "modulo:acao")] como um Permissao.Codigo
// a ser checado por PermissaoAuthorizationHandler — dispensa registrar cada código individualmente
// via AddPolicy em Program.cs. Só entra em uso quando autenticacaoEntraIdHabilitada=true (ver
// Program.cs); com o Entra ID desligado, nem este provider nem UseAuthorization() são registrados.
public class PermissaoAuthorizationPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _padrao;

    public PermissaoAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _padrao = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var politica = new AuthorizationPolicyBuilder()
            .AddRequirements(new PermissaoRequirement(policyName))
            .Build();
        return Task.FromResult<AuthorizationPolicy?>(politica);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _padrao.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _padrao.GetFallbackPolicyAsync();
}

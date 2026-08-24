using Microsoft.AspNetCore.Authorization;

namespace AAHBRANT.SST.Api.Autorizacao;

// Um requirement por Permissao.Codigo (ex.: "apr:aprovar"). Não há um requirement por controller —
// o nome da policy passada em [Authorize(Policy = "...")] É o próprio código da permissão, resolvido
// dinamicamente por PermissaoAuthorizationPolicyProvider (evita precisar registrar ~40 AddPolicy
// manualmente e cadastrar de novo cada vez que a tela de Perfis & Matriz de Permissões ganhar um
// código novo).
public class PermissaoRequirement : IAuthorizationRequirement
{
    public string Codigo { get; }

    public PermissaoRequirement(string codigo) => Codigo = codigo;
}

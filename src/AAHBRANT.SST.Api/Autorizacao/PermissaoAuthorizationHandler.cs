using System.Security.Claims;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Api.Autorizacao;

// Camada 1 do desenho de docs/RBAC-Matrix.md §4 (policy-based por perfil): confirma que o usuário
// autenticado tem, em ALGUM PerfilAcesso vinculado a ele (qualquer obra), uma PerfilAcessoPermissao
// com Permitido=true para o código exigido. Não checa o escopo por obra especificamente (Camada 2
// do desenho, "Handler de escopo por obra") nem aplica o Global Query Filter (Camada 3) — as duas
// ficam pendentes deliberadamente: exigem primeiro threading do contexto de obra por requisição e a
// validação da matriz por Diretoria/Gestor QSMS (RBAC-Matrix.md §5), então não foram implementadas
// junto com esta camada básica.
//
// Descoberta empírica ao ligar [Authorize] nos controllers (não é comportamento assumido/documentado
// a priori): mesmo com autenticacaoEntraIdHabilitada=false, o ASP.NET Core já registra por padrão um
// IAuthorizationPolicyProvider/IAuthorizationService mínimos (necessários para o próprio [Authorize]
// funcionar em minimal hosting), e insere Authentication/AuthorizationMiddleware no pipeline mesmo sem
// UseAuthentication()/UseAuthorization() explícitos. Isso significa que só registrar ESTE handler
// condicionalmente (como Program.cs fazia antes) não é suficiente para não quebrar nada hoje: (1) se o
// handler não está registrado, o provider default não reconhece o nome da policy e lança
// InvalidOperationException por request; (2) mesmo registrando sempre o handler, sem um usuário
// autenticado (Entra ID desligado) o requirement nunca seria satisfeito, dando 403 em vez de passar.
// Por isso o próprio handler agora checa a mesma flag de Program.cs e libera (Succeed) sem checagem
// real enquanto o Entra ID não estiver configurado — item que precisa ser destacado ao usuário porque
// muda o sentido de "estrutura pronta, sem ligar": os atributos [Authorize] ficam OPERANTES no código,
// mas o handler é que se comporta como no-op até o TenantId existir.
public class PermissaoAuthorizationHandler : AuthorizationHandler<PermissaoRequirement>
{
    private readonly IAppDbContext _db;
    private readonly IConfiguration _configuracao;

    public PermissaoAuthorizationHandler(IAppDbContext db, IConfiguration configuracao)
    {
        _db = db;
        _configuracao = configuracao;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissaoRequirement requirement)
    {
        var autenticacaoEntraIdHabilitada = !string.IsNullOrWhiteSpace(_configuracao["AzureAd:TenantId"]);
        if (!autenticacaoEntraIdHabilitada)
        {
            context.Succeed(requirement);
            return;
        }

        // "oid" é o claim padrão do Entra ID para o Object Id do usuário no tenant; ClaimTypes.NameIdentifier
        // cobre o fallback de outros provedores de identidade compatíveis com OpenID Connect.
        var azureAdObjectId = context.User.FindFirst("oid")?.Value
            ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(azureAdObjectId))
        {
            return;
        }

        var temPermissao = await _db.Usuarios
            .Where(u => u.AzureAdObjectId == azureAdObjectId && u.Status == StatusUsuario.Ativo)
            .SelectMany(u => u.PerfisPorObra)
            .Select(vinculo => vinculo.PerfilAcesso)
            .Where(perfil => perfil != null)
            .SelectMany(perfil => perfil!.Permissoes)
            .AnyAsync(pp => pp.Permitido && pp.Permissao != null && pp.Permissao.Codigo == requirement.Codigo);

        if (temPermissao)
        {
            context.Succeed(requirement);
        }
    }
}

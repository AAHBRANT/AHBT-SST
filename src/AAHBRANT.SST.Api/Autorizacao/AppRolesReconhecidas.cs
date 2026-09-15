using System.Security.Claims;

namespace AAHBRANT.SST.Api.Autorizacao;

// Integração G-RH (10/09): tokens client-credentials (app-only) não têm usuário humano por trás — a
// App Role chega no claim "roles"/ClaimTypes.Role, não em "oid" vinculável a um Usuario. Fonte única
// para as duas camadas que hoje só sabiam reconhecer usuário humano: PermissaoAuthorizationHandler
// (Camada 1 — permissão) e EscopoPorObraMiddleware (Camada 3 — escopo de obra). Hoje só existe uma
// App Role reconhecida: Grh.LerColaboradores, com acesso global (o G-RH consulta trabalhadores de
// qualquer obra, não só uma) e liberando toda a policy "trabalhador:ver" — decisão de escopo de
// 10/09, aceita porque o G-RH já é a fonte original desses dados (inclusive CPF).
public static class AppRolesReconhecidas
{
    private static readonly Dictionary<string, string[]> PermissoesPorAppRole = new()
    {
        ["Grh.LerColaboradores"] = new[] { "trabalhador:ver" },
    };

    private static IEnumerable<string> ObterRoles(ClaimsPrincipal user) =>
        user.FindAll("roles").Select(c => c.Value).Concat(user.FindAll(ClaimTypes.Role).Select(c => c.Value));

    public static bool TemPermissao(ClaimsPrincipal user, string codigoPermissao) =>
        ObterRoles(user).Any(role => PermissoesPorAppRole.TryGetValue(role, out var codigos) && codigos.Contains(codigoPermissao));

    public static bool TemAcessoGlobal(ClaimsPrincipal user) =>
        ObterRoles(user).Any(PermissoesPorAppRole.ContainsKey);
}

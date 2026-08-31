using System.Security.Claims;
using AAHBRANT.SST.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Api.Middlewares;

// Camada 3 de docs/RBAC-Matrix.md §4 (Global Query Filter): resolve, uma vez por requisição e
// ANTES de qualquer controller/DbContext ser usado, se o usuário atual tem acesso global ou está
// restrito a um conjunto de obras — e guarda o resultado em ICurrentUserService (Scoped), que o
// filtro global em SstDbContext consulta em toda consulta às entidades com ObraId direto (Dds,
// Inspecao, Acidente, Pgr, Atividade, Setor, Trabalhador, AreaSst).
//
// Registrado DEPOIS de UseAuthentication/UseAuthorization no pipeline (Program.cs) — precisa de
// contexto.User já resolvido. Enquanto a autenticação Entra ID estiver desligada (mesma flag de
// PermissaoAuthorizationHandler), mantém TemAcessoGlobal=true — ou seja, sem nenhuma mudança de
// comportamento hoje. Passa a restringir de verdade automaticamente no momento em que o Entra ID
// for configurado, sem precisar mexer neste arquivo.
public class EscopoPorObraMiddleware
{
    private readonly RequestDelegate _proximo;

    public EscopoPorObraMiddleware(RequestDelegate proximo)
    {
        _proximo = proximo;
    }

    public async Task InvokeAsync(HttpContext contexto, IAppDbContext db, ICurrentUserService usuarioAtual, IConfiguration configuracao)
    {
        var autenticacaoEntraIdHabilitada = !string.IsNullOrWhiteSpace(configuracao["AzureAd:TenantId"]);
        if (!autenticacaoEntraIdHabilitada)
        {
            usuarioAtual.DefinirEscopo(temAcessoGlobal: true, Array.Empty<Guid>());
            await _proximo(contexto);
            return;
        }

        var azureAdObjectId = contexto.User.FindFirst("oid")?.Value
            ?? contexto.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(azureAdObjectId))
        {
            // Sem identidade resolvível: nega por padrão (não dá acesso global) — quem barra a
            // requisição de fato é o PermissaoAuthorizationHandler; isto só evita vazar dado de
            // obra alheia caso algum endpoint não exija [Authorize].
            usuarioAtual.DefinirEscopo(temAcessoGlobal: false, Array.Empty<Guid>());
            await _proximo(contexto);
            return;
        }

        var obrasVinculadas = await db.UsuariosPerfilObra
            .Where(v => v.Usuario != null && v.Usuario.AzureAdObjectId == azureAdObjectId)
            .Select(v => v.ObraId)
            .ToListAsync();

        var temAcessoGlobal = obrasVinculadas.Any(obraId => obraId == null);
        var obrasPermitidas = obrasVinculadas.Where(obraId => obraId.HasValue).Select(obraId => obraId!.Value).ToList();

        usuarioAtual.DefinirEscopo(temAcessoGlobal, obrasPermitidas);
        await _proximo(contexto);
    }
}

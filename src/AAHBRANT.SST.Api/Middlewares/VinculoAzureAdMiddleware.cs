using System.Security.Claims;
using AAHBRANT.SST.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Api.Middlewares;

// O Administrador nunca digita o Azure AD Object Id ao cadastrar um usuário — ele não tem como
// conhecer o `oid` de outra identidade no Entra ID. O cadastro manual (CriarUsuarioCommand) grava
// só Email/Nome; este middleware, executado a cada request autenticado via Teams SSO, vincula
// automaticamente o usuário pré-cadastrado (Email igual, AzureAdObjectId ainda nulo) ao `oid` do
// token recebido no primeiro login.
public class VinculoAzureAdMiddleware
{
    private readonly RequestDelegate _proximo;

    public VinculoAzureAdMiddleware(RequestDelegate proximo) => _proximo = proximo;

    public async Task InvokeAsync(HttpContext contexto, IAppDbContext db)
    {
        if (contexto.User.Identity?.IsAuthenticated == true)
        {
            var oid = contexto.User.FindFirst("oid")?.Value
                ?? contexto.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrWhiteSpace(oid))
            {
                var usuario = await db.Usuarios
                    .FirstOrDefaultAsync(u => u.AzureAdObjectId == oid, contexto.RequestAborted);

                if (usuario is null)
                {
                    var email = contexto.User.FindFirst("preferred_username")?.Value
                        ?? contexto.User.FindFirst(ClaimTypes.Email)?.Value;

                    if (!string.IsNullOrWhiteSpace(email))
                    {
                        usuario = await db.Usuarios.FirstOrDefaultAsync(
                            u => u.AzureAdObjectId == null && u.Email == email, contexto.RequestAborted);
                        if (usuario is not null)
                        {
                            usuario.AzureAdObjectId = oid;
                        }
                    }
                }

                if (usuario is not null)
                {
                    usuario.UltimoLoginUtc = DateTime.UtcNow;
                    await db.SaveChangesAsync(contexto.RequestAborted);
                }
            }
        }

        await _proximo(contexto);
    }
}

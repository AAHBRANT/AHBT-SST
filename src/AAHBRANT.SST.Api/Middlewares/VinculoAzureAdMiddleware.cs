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

    public async Task InvokeAsync(HttpContext contexto, IAppDbContext db, ILogger<VinculoAzureAdMiddleware> logger)
    {
        if (contexto.User.Identity?.IsAuthenticated == true)
        {
            // Nunca deixa uma falha aqui (claim inesperada, coluna estourada, etc.) derrubar a
            // request inteira — vínculo automático é um "nice to have" de cada chamada, não algo
            // que deva bloquear o usuário de usar o app. Descoberto ao vivo: uma exceção não
            // tratada aqui quebrava 100% das requests autenticadas.
            try
            {
                // Só GUID (36 chars) cabe na coluna AzureAdObjectId (nvarchar(36)) — o claim "oid"
                // do Entra ID sempre é GUID; qualquer outra coisa aqui é sinal de mapeamento de
                // claim errado (ver MapInboundClaims em Program.cs) e é melhor ignorar que estourar.
                var oid = contexto.User.FindFirst("oid")?.Value
                    ?? contexto.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (!string.IsNullOrWhiteSpace(oid) && Guid.TryParse(oid, out _))
                {
                    // ExecuteUpdateAsync (sem tracking, sem checar RowVersion) em vez de carregar a
                    // entidade e chamar SaveChangesAsync: este middleware roda em TODA request
                    // autenticada, inclusive rajadas de chamadas paralelas do mesmo usuário (ex.: o
                    // Promise.all de 11 chamadas do Dashboard) — descoberto ao vivo que essas rajadas
                    // colidiam entre si no token de concorrência (RowVersion) do Usuario, cada
                    // request perdendo pra outra com DbUpdateConcurrencyException. Um timestamp de
                    // "último login" pode perder uma escrita concorrente sem problema nenhum.
                    var jaVinculado = await db.Usuarios
                        .AnyAsync(u => u.AzureAdObjectId == oid, contexto.RequestAborted);

                    if (jaVinculado)
                    {
                        await db.Usuarios
                            .Where(u => u.AzureAdObjectId == oid)
                            .ExecuteUpdateAsync(
                                s => s.SetProperty(u => u.UltimoLoginUtc, DateTime.UtcNow),
                                contexto.RequestAborted);
                    }
                    else
                    {
                        var email = contexto.User.FindFirst("preferred_username")?.Value
                            ?? contexto.User.FindFirst(ClaimTypes.Email)?.Value;

                        if (!string.IsNullOrWhiteSpace(email))
                        {
                            await db.Usuarios
                                .Where(u => u.AzureAdObjectId == null && u.Email == email)
                                .ExecuteUpdateAsync(
                                    s => s
                                        .SetProperty(u => u.AzureAdObjectId, oid)
                                        .SetProperty(u => u.UltimoLoginUtc, DateTime.UtcNow),
                                    contexto.RequestAborted);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Falha ao vincular AzureAdObjectId automaticamente — request segue sem vínculo.");
            }
        }

        await _proximo(contexto);
    }
}

using System.Security.Claims;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Api.Autorizacao;

// Quem assina/aprova é SEMPRE o usuário logado (pedido do usuário, 29/09): o frontend nunca envia
// nem pede o Id de usuário — a identidade sai do claim do token, mesma regra de UsuariosController.Eu.
// Um Id vindo do corpo permitiria assinar em nome de outra pessoa.
public interface IUsuarioAtualResolver
{
    // Lança InvalidOperationException (vira 400 com mensagem legível) se não houver usuário ativo.
    Task<Guid> ObterIdAsync(ClaimsPrincipal principal, CancellationToken ct);
}

public class UsuarioAtualResolver : IUsuarioAtualResolver
{
    private readonly IAppDbContext _db;
    private readonly IConfiguration _configuracao;

    public UsuarioAtualResolver(IAppDbContext db, IConfiguration configuracao)
    {
        _db = db;
        _configuracao = configuracao;
    }

    public async Task<Guid> ObterIdAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        var autenticacaoHabilitada = !string.IsNullOrWhiteSpace(_configuracao["AzureAd:TenantId"]);

        if (!autenticacaoHabilitada)
        {
            // Entra ID desligado (desenvolvimento local): não há token para identificar ninguém.
            // Usa o primeiro usuário ativo, para o fluxo poder ser testado; em produção este ramo
            // nunca roda, pois o TenantId sempre está configurado.
            var idDev = await _db.Usuarios.AsNoTracking()
                .Where(u => u.Status == StatusUsuario.Ativo)
                .OrderBy(u => u.CreatedAtUtc)
                .Select(u => (Guid?)u.Id)
                .FirstOrDefaultAsync(ct);
            return idDev ?? throw new InvalidOperationException(
                "Nenhum usuário ativo cadastrado para assinar o documento.");
        }

        var azureAdObjectId = principal.FindFirst("oid")?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        var id = string.IsNullOrWhiteSpace(azureAdObjectId)
            ? null
            : await _db.Usuarios.AsNoTracking()
                .Where(u => u.AzureAdObjectId == azureAdObjectId && u.Status == StatusUsuario.Ativo)
                .Select(u => (Guid?)u.Id)
                .FirstOrDefaultAsync(ct);

        return id ?? throw new InvalidOperationException(
            "Seu usuário não está cadastrado ou ativo no sistema. Procure o administrador para assinar.");
    }
}

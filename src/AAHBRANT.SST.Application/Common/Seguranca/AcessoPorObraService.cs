using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Common.Seguranca;

public sealed record EscopoPermissao(bool Global, IReadOnlyList<Guid> Obras)
{
    public bool TemPermissao => Global || Obras.Count > 0;
    public bool Permite(Guid obraId) => Global || Obras.Contains(obraId);

    public EscopoPermissao Intersectar(EscopoPermissao outro) => Global ? outro : outro.Global ? this
        : new(false, Obras.Intersect(outro.Obras).ToArray());
}

public interface IAcessoPorObraService
{
    Task<EscopoPermissao> ObterEscopoAsync(string? objectId, string permissao, CancellationToken ct);
}

// O vínculo que concede a ação é o mesmo que define suas obras. Um perfil global de consulta
// nunca amplia a edição concedida por outro perfil restrito. Cache somente durante a requisição.
public sealed class AcessoPorObraService(IAppDbContext db) : IAcessoPorObraService
{
    private readonly Dictionary<(string, string), EscopoPermissao> _cache = new();

    public async Task<EscopoPermissao> ObterEscopoAsync(string? objectId, string permissao, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(objectId)) return new(false, Array.Empty<Guid>());
        if (_cache.TryGetValue((objectId, permissao), out var escopo)) return escopo;

        var obras = await db.UsuariosPerfilObra.AsNoTracking()
            .Where(v => v.Ativo && v.Usuario != null && v.Usuario.Ativo
                && v.Usuario.Status == StatusUsuario.Ativo && v.Usuario.AzureAdObjectId == objectId
                && v.PerfilAcesso != null && v.PerfilAcesso.Ativo
                && v.PerfilAcesso.Permissoes.Any(p => p.Ativo && p.Permitido
                    && p.Permissao != null && p.Permissao.Ativo && p.Permissao.Codigo == permissao))
            .Select(v => v.ObraId).Distinct().ToListAsync(ct);

        escopo = new(obras.Contains(null), obras.Where(id => id.HasValue).Select(id => id!.Value).ToArray());
        _cache[(objectId, permissao)] = escopo;
        return escopo;
    }
}

public sealed class AcessoNegadoException() : Exception("Você não tem permissão para acessar este recurso nesta obra.");

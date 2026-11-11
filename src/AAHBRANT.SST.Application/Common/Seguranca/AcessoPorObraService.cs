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

        // Coluna de alcance da matriz (06/11/2026): "Global" na linha Perfil x Permissão vale em todas
        // as obras, mesmo com o perfil atribuído a uma obra só — é o que deixa o técnico responsável
        // pela própria obra VER as demais (supervisão) sem poder EDITAR nelas. As outras colunas
        // (Obra, Unidade, Próprio) ficam restritas à obra do vínculo; vínculo sem obra continua global.
        var vinculos = await db.UsuariosPerfilObra.AsNoTracking()
            .Where(v => v.Ativo && v.Usuario != null && v.Usuario.Ativo
                && v.Usuario.Status == StatusUsuario.Ativo && v.Usuario.AzureAdObjectId == objectId
                && v.PerfilAcesso != null && v.PerfilAcesso.Ativo)
            .Select(v => new
            {
                v.ObraId,
                Escopos = v.PerfilAcesso!.Permissoes
                    .Where(p => p.Ativo && p.Permitido && p.Permissao != null && p.Permissao.Ativo && p.Permissao.Codigo == permissao)
                    .Select(p => p.Escopo).ToList()
            })
            .ToListAsync(ct);

        var concedidos = vinculos.Where(v => v.Escopos.Count > 0).ToList();
        var global = concedidos.Any(v => v.ObraId == null || v.Escopos.Contains(EscopoAcesso.Global));
        escopo = new(global, concedidos.Where(v => v.ObraId.HasValue).Select(v => v.ObraId!.Value).Distinct().ToArray());
        _cache[(objectId, permissao)] = escopo;
        return escopo;
    }
}

public sealed class AcessoNegadoException : Exception
{
    public AcessoNegadoException() : base("Você não tem permissão para acessar este recurso nesta obra.") { }

    public AcessoNegadoException(string mensagem) : base(mensagem) { }
}

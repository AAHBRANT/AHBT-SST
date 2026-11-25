using AAHBRANT.SST.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Common;

// Foto do responsável nas listas (25/11/2026): para cada usuário ou funcionário, o nome, o funcionário
// vinculado e se ele tem foto de perfil. Consulta leve: nunca carrega a imagem (só testa se existe),
// porque a tela baixa a foto à parte, uma vez por pessoa (GET /api/trabalhadores/{id}/foto).
public record PessoaComFoto(string Nome, Guid? TrabalhadorId, bool TemFoto);

public static class FotosPessoas
{
    public static async Task<Dictionary<Guid, PessoaComFoto>> DeUsuariosAsync(IAppDbContext db, IEnumerable<Guid?> usuarioIds, CancellationToken ct)
    {
        var ids = usuarioIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        if (ids.Count == 0) return new();
        return await db.Usuarios.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new
            {
                u.Id,
                u.Nome,
                u.TrabalhadorId,
                TemFoto = u.Trabalhador != null && u.Trabalhador.FotoConteudo != null,
            })
            .ToDictionaryAsync(u => u.Id, u => new PessoaComFoto(u.Nome, u.TrabalhadorId, u.TemFoto), ct);
    }

    public static async Task<Dictionary<Guid, PessoaComFoto>> DeTrabalhadoresAsync(IAppDbContext db, IEnumerable<Guid?> trabalhadorIds, CancellationToken ct)
    {
        var ids = trabalhadorIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        if (ids.Count == 0) return new();
        return await db.Trabalhadores.AsNoTracking()
            .Where(t => ids.Contains(t.Id))
            .Select(t => new { t.Id, t.Nome, TemFoto = t.FotoConteudo != null })
            .ToDictionaryAsync(t => t.Id, t => new PessoaComFoto(t.Nome, t.Id, t.TemFoto), ct);
    }
}

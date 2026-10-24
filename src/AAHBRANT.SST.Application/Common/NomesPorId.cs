using AAHBRANT.SST.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Common;

// Nomes de função/perigo/usuário por id, numa consulta SEPARADA com IgnoreQueryFilters (mostra o nome
// mesmo do registro excluído). Nunca usar IgnoreQueryFilters dentro de outra consulta: no EF Core ele
// vale para a consulta inteira e desliga também os filtros de Ativo e de obra da consulta principal
// (bug achado em 10/10/2026 — GHE desativados voltavam na listagem).
public static class NomesPorId
{
    public static async Task<Dictionary<Guid, (string Nome, string? Cbo)>> FuncoesAsync(IAppDbContext db, IEnumerable<Guid> ids, CancellationToken ct)
    {
        var lista = ids.Distinct().ToList();
        return await db.Funcoes.IgnoreQueryFilters().Where(f => lista.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => (f.Nome, f.CboCodigo), ct);
    }

    public static async Task<Dictionary<Guid, (string Nome, string? Agente)>> PerigosAsync(IAppDbContext db, IEnumerable<Guid> ids, CancellationToken ct)
    {
        var lista = ids.Distinct().ToList();
        return await db.Perigos.IgnoreQueryFilters().Where(p => lista.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => (p.Nome, p.Agente), ct);
    }

    public static async Task<Dictionary<Guid, string>> UsuariosAsync(IAppDbContext db, IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var lista = ids.Where(i => i.HasValue).Select(i => i!.Value).Distinct().ToList();
        return await db.Usuarios.IgnoreQueryFilters().Where(u => lista.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Nome, ct);
    }
}

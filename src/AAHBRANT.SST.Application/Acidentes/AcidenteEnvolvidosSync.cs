using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Acidentes;

// Resolve e grava a lista de funcionários envolvidos. A lista nova (TrabalhadoresIds) tem
// precedência; TrabalhadorId isolado continua aceito para clientes antigos. O primeiro da lista
// vira Acidente.TrabalhadorId (envolvido principal, enviado ao G-RH).
public static class AcidenteEnvolvidosSync
{
    public static List<Guid> Resolver(IEnumerable<Guid>? trabalhadoresIds, Guid? trabalhadorId)
    {
        var ids = (trabalhadoresIds ?? Enumerable.Empty<Guid>()).Where(i => i != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0 && trabalhadorId.HasValue && trabalhadorId.Value != Guid.Empty)
            ids.Add(trabalhadorId.Value);
        return ids;
    }

    // Duas consultas simples em vez de JOIN: um Trabalhador filtrado/desligado não pode esconder a
    // linha do envolvido. Os nomes vêm sem query filter porque o histórico da ocorrência deve
    // continuar legível mesmo depois do desligamento.
    public static async Task<Dictionary<Guid, List<AcidenteEnvolvidoDto>>> CarregarAsync(
        IAppDbContext db, IReadOnlyCollection<Guid> acidenteIds, CancellationToken ct)
    {
        var vinculos = await db.AcidentesEnvolvidos.AsNoTracking()
            .Where(e => acidenteIds.Contains(e.AcidenteId))
            .OrderBy(e => e.CreatedAtUtc)
            .Select(e => new { e.AcidenteId, e.TrabalhadorId })
            .ToListAsync(ct);

        var trabalhadorIds = vinculos.Select(v => v.TrabalhadorId).Distinct().ToList();
        var nomes = await db.Trabalhadores.IgnoreQueryFilters().AsNoTracking()
            .Where(t => trabalhadorIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Nome })
            .ToDictionaryAsync(t => t.Id, t => t.Nome, ct);

        return vinculos
            .GroupBy(v => v.AcidenteId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(v => new AcidenteEnvolvidoDto(v.TrabalhadorId, nomes.GetValueOrDefault(v.TrabalhadorId, "—"))).ToList());
    }

    public static async Task ValidarAsync(IAppDbContext db, List<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return;
        var existentes = await db.Trabalhadores.CountAsync(t => ids.Contains(t.Id), ct);
        if (existentes != ids.Count)
            throw new KeyNotFoundException("Um ou mais funcionários envolvidos não foram encontrados.");
    }

    public static async Task SincronizarAsync(IAppDbContext db, Acidente acidente, List<Guid> ids, CancellationToken ct)
    {
        acidente.TrabalhadorId = ids.Count > 0 ? ids[0] : null;

        var atuais = await db.AcidentesEnvolvidos.Where(e => e.AcidenteId == acidente.Id).ToListAsync(ct);
        db.AcidentesEnvolvidos.RemoveRange(atuais.Where(e => !ids.Contains(e.TrabalhadorId)));
        var jaExistem = atuais.Select(e => e.TrabalhadorId).ToHashSet();
        db.AcidentesEnvolvidos.AddRange(ids.Where(i => !jaExistem.Contains(i)).Select(i => new AcidenteEnvolvido
        {
            AcidenteId = acidente.Id,
            TrabalhadorId = i,
        }));
    }
}

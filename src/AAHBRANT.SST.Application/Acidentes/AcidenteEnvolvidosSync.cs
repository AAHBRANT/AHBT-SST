using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Acidentes;

// Resolve e grava a lista de funcionários da ocorrência. A lista nova (TrabalhadoresIds) tem
// precedência; TrabalhadorId isolado continua aceito para clientes antigos. O primeiro da lista
// vira Acidente.TrabalhadorId (envolvido principal, enviado ao G-RH). Cada funcionário tem um papel:
// Envolvido (a lista acima, sempre) ou Testemunha (lista à parte; nunca vira envolvido principal nem
// vai ao G-RH).
public static class AcidenteEnvolvidosSync
{
    public static List<Guid> Resolver(IEnumerable<Guid>? trabalhadoresIds, Guid? trabalhadorId)
    {
        var ids = (trabalhadoresIds ?? Enumerable.Empty<Guid>()).Where(i => i != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0 && trabalhadorId.HasValue && trabalhadorId.Value != Guid.Empty)
            ids.Add(trabalhadorId.Value);
        return ids;
    }

    // Testemunhas: null (cliente que não manda o campo) continua null = "não mexe".
    public static List<Guid>? ResolverTestemunhas(IEnumerable<Guid>? testemunhasIds) =>
        testemunhasIds?.Where(i => i != Guid.Empty).Distinct().ToList();

    // Duas consultas simples em vez de JOIN: um Trabalhador filtrado/desligado não pode esconder a
    // linha do envolvido. Os nomes vêm sem query filter porque o histórico da ocorrência deve
    // continuar legível mesmo depois do desligamento. Por padrão devolve só quem tem papel Envolvido;
    // as testemunhas saem por CarregarTestemunhasAsync (consumidores antigos, como as regras de
    // resolução do plano de ação, não podem tratar testemunha como envolvido).
    public static Task<Dictionary<Guid, List<AcidenteEnvolvidoDto>>> CarregarAsync(
        IAppDbContext db, IReadOnlyCollection<Guid> acidenteIds, CancellationToken ct) =>
        CarregarPorPapelAsync(db, acidenteIds, PapelEnvolvido.Envolvido, ct);

    public static Task<Dictionary<Guid, List<AcidenteEnvolvidoDto>>> CarregarTestemunhasAsync(
        IAppDbContext db, IReadOnlyCollection<Guid> acidenteIds, CancellationToken ct) =>
        CarregarPorPapelAsync(db, acidenteIds, PapelEnvolvido.Testemunha, ct);

    private static async Task<Dictionary<Guid, List<AcidenteEnvolvidoDto>>> CarregarPorPapelAsync(
        IAppDbContext db, IReadOnlyCollection<Guid> acidenteIds, PapelEnvolvido papel, CancellationToken ct)
    {
        var vinculos = await db.AcidentesEnvolvidos.AsNoTracking()
            .Where(e => acidenteIds.Contains(e.AcidenteId) && e.Papel == papel)
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
                g => g.Select(v => new AcidenteEnvolvidoDto(v.TrabalhadorId, nomes.GetValueOrDefault(v.TrabalhadorId, "—"), papel)).ToList());
    }

    public static async Task ValidarAsync(IAppDbContext db, List<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return;
        var existentes = await db.Trabalhadores.CountAsync(t => ids.Contains(t.Id), ct);
        if (existentes != ids.Count)
            throw new KeyNotFoundException("Um ou mais funcionários envolvidos não foram encontrados.");
    }

    // Mesmo funcionário não pode ser envolvido e testemunha (a tabela tem um vínculo por funcionário).
    public static void ValidarSemSobreposicao(List<Guid> envolvidos, List<Guid>? testemunhas)
    {
        if (testemunhas is not null && testemunhas.Any(envolvidos.Contains))
            throw new InvalidOperationException("Um funcionário não pode ser envolvido e testemunha da mesma ocorrência.");
    }

    // testemunhas == null: mantém as testemunhas já gravadas (só os envolvidos são sincronizados).
    public static async Task SincronizarAsync(
        IAppDbContext db, Acidente acidente, List<Guid> ids, CancellationToken ct, List<Guid>? testemunhas = null)
    {
        // O principal enviado ao G-RH é sempre um Envolvido — testemunha nunca ocupa esse lugar.
        acidente.TrabalhadorId = ids.Count > 0 ? ids[0] : null;

        var atuais = await db.AcidentesEnvolvidos.Where(e => e.AcidenteId == acidente.Id).ToListAsync(ct);
        var desejados = ids.Select(i => (Id: i, Papel: PapelEnvolvido.Envolvido)).ToList();
        if (testemunhas is not null)
            desejados.AddRange(testemunhas.Select(i => (Id: i, Papel: PapelEnvolvido.Testemunha)));

        var idsDesejados = desejados.Select(d => d.Id).ToHashSet();
        db.AcidentesEnvolvidos.RemoveRange(atuais.Where(e =>
            !idsDesejados.Contains(e.TrabalhadorId)
            && (testemunhas is not null || e.Papel == PapelEnvolvido.Envolvido)));

        foreach (var (id, papel) in desejados)
        {
            var existente = atuais.FirstOrDefault(e => e.TrabalhadorId == id);
            if (existente is null)
                db.AcidentesEnvolvidos.Add(new AcidenteEnvolvido { AcidenteId = acidente.Id, TrabalhadorId = id, Papel = papel });
            else if (existente.Papel != papel)
                existente.Papel = papel;
        }
    }
}

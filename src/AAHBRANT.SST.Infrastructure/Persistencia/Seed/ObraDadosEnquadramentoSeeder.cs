using System.Text.Json;
using AAHBRANT.SST.Application.GeracaoPgr;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Seed;

// Migra para o cadastro da Obra (CEP, CNAE e Grau de Risco) os valores que já haviam sido preenchidos
// no PGR ou no Dimensionamento da CIPA, garantindo que obras já existentes fiquem completas sem redigitar.
public static class ObraDadosEnquadramentoSeeder
{
    private static readonly JsonSerializerOptions OpcoesJson = new(JsonSerializerDefaults.Web);

    public static async Task ExecutarAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SstDbContext>();
        await PreencherObrasAsync(db, ct);
    }

    public static async Task PreencherObrasAsync(SstDbContext db, CancellationToken ct = default)
    {
        var obras = await db.Obras
            .Where(o => o.Cep == null || o.Cnae == null || o.GrauRisco == null)
            .ToListAsync(ct);
        if (obras.Count == 0) return;

        var obraIds = obras.Select(o => o.Id).ToList();
        var geracoes = await db.GeracoesPgrEap
            .AsNoTracking()
            .Where(g => obraIds.Contains(g.ObraId))
            .Select(g => new { g.ObraId, g.Status, g.AprovadoEmUtc, g.CreatedAtUtc, g.EstruturaJson })
            .ToListAsync(ct);

        var cipas = await db.DimensionamentosCipa
            .AsNoTracking()
            .Where(d => obraIds.Contains(d.ObraId))
            .OrderByDescending(d => d.DataCalculo)
            .Select(d => new { d.ObraId, d.Cnae, d.GrauRisco })
            .ToListAsync(ct);

        var alterou = false;
        foreach (var obra in obras)
        {
            var doPgr = geracoes
                .Where(g => g.ObraId == obra.Id)
                .OrderBy(g => g.Status == StatusGeracaoPgrEap.Aprovado ? 0 : 1)
                .ThenByDescending(g => g.AprovadoEmUtc ?? g.CreatedAtUtc)
                .ToList();

            foreach (var g in doPgr)
            {
                var comp = LerComplementares(g.EstruturaJson);
                if (comp is null) continue;
                if (string.IsNullOrWhiteSpace(obra.Cep) && !string.IsNullOrWhiteSpace(comp.Cep))
                {
                    obra.Cep = comp.Cep.Trim()[..Math.Min(comp.Cep.Trim().Length, 10)];
                    alterou = true;
                }
                if (string.IsNullOrWhiteSpace(obra.Cnae) && !string.IsNullOrWhiteSpace(comp.Cnae))
                {
                    obra.Cnae = comp.Cnae.Trim()[..Math.Min(comp.Cnae.Trim().Length, 200)];
                    alterou = true;
                }
                if (obra.GrauRisco is null && comp.GrauRisco is >= 1 and <= 4)
                {
                    obra.GrauRisco = comp.GrauRisco;
                    alterou = true;
                }
            }

            var daCipa = cipas.FirstOrDefault(c => c.ObraId == obra.Id);
            if (daCipa is not null)
            {
                if (string.IsNullOrWhiteSpace(obra.Cnae) && !string.IsNullOrWhiteSpace(daCipa.Cnae))
                {
                    obra.Cnae = daCipa.Cnae.Trim();
                    alterou = true;
                }
                if (obra.GrauRisco is null && daCipa.GrauRisco is >= 1 and <= 4)
                {
                    obra.GrauRisco = daCipa.GrauRisco;
                    alterou = true;
                }
            }
        }

        if (alterou)
            await db.SaveChangesAsync(ct);
    }

    private static DadosComplementaresPgr? LerComplementares(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<EstruturaGeracaoPgr>(json, OpcoesJson)?.Complementares;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

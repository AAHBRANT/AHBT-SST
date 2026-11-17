using AAHBRANT.SST.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.IntegracoesAtendimento;

// Quem tem digital e/ou facial cadastrada (o instrutor só assina com uma delas). Consulta simples por tabela.
public static class BiometriaInstrutor
{
    public static async Task<(HashSet<Guid> ComDigital, HashSet<Guid> ComFacial)> CarregarAsync(
        IAppDbContext db, IReadOnlyCollection<Guid> trabalhadorIds, CancellationToken ct)
    {
        var ids = trabalhadorIds.ToList();
        var digital = (await db.TemplatesBiometricoFutronic.Where(b => ids.Contains(b.TrabalhadorId))
            .Select(b => b.TrabalhadorId).Distinct().ToListAsync(ct)).ToHashSet();
        var facial = (await db.FotosCadastroFacial.Where(f => ids.Contains(f.TrabalhadorId))
            .Select(f => f.TrabalhadorId).Distinct().ToListAsync(ct)).ToHashSet();
        return (digital, facial);
    }
}

using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Seed;

// Idempotente (mesmo padrão de ChecklistAlojamentoSeeder/RegraAlertaSeeder): garante que sempre
// exista exatamente uma linha de configuração, com o padrão de 30 dias caso ninguém tenha mexido.
public static class ConfiguracaoAlojamentoSeeder
{
    public static async Task ExecutarAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SstDbContext>();

        var jaExiste = await db.ConfiguracoesAlojamento.IgnoreQueryFilters().AnyAsync(ct);
        if (jaExiste) return;

        db.ConfiguracoesAlojamento.Add(new ConfiguracaoAlojamento { DiasParaInspecaoAtrasada = 30 });
        await db.SaveChangesAsync(ct);
    }
}

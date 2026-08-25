using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Seed;

// Seeder idempotente (mesmo padrão do RbacSeeder) que garante um ponto de partida configurável
// para o Motor Central de Alertas — 30/15/7 dias de antecedência (Info/Atencao/Critico), pedido
// explícito do usuário em 2026-08-24 ("a regra deve ser configurável", não hardcoded). Roda só na
// primeira vez por módulo: se o usuário já editou/removeu as regras de um módulo pela tela de
// Administração, este seeder não insere nada de volta (checa por Modulo, não por linha exata).
public static class RegraAlertaSeeder
{
    private static readonly TipoModuloAlerta[] ModulosComRegraPadrao =
    {
        TipoModuloAlerta.Aso,
        TipoModuloAlerta.Treinamento,
        TipoModuloAlerta.Higienizacao,
        TipoModuloAlerta.Extintor,
        TipoModuloAlerta.Equipamento,
    };

    public static async Task ExecutarAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SstDbContext>();

        var modulosExistentes = await db.RegrasAlerta
            .IgnoreQueryFilters()
            .Select(r => r.Modulo)
            .Distinct()
            .ToListAsync(ct);

        foreach (var modulo in ModulosComRegraPadrao)
        {
            if (modulosExistentes.Contains(modulo)) continue;

            db.RegrasAlerta.AddRange(
                new RegraAlerta { Modulo = modulo, DiasAntecedencia = 30, Severidade = SeveridadeAlerta.Info },
                new RegraAlerta { Modulo = modulo, DiasAntecedencia = 15, Severidade = SeveridadeAlerta.Atencao },
                new RegraAlerta { Modulo = modulo, DiasAntecedencia = 7, Severidade = SeveridadeAlerta.Critico });
        }

        await db.SaveChangesAsync(ct);
    }
}

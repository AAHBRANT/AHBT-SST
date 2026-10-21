using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Seed;

// Carga inicial das regras "quando uma NR passa a ser obrigatória" (21/10). Proposta do sistema:
// o QSMS valida e ajusta pela tela Treinamentos › Regras. Só roda com a tabela vazia (contando as
// desativadas), então o que o QSMS mudou ou desativou nunca volta.
public static class RegrasCursoObrigatorioSeeder
{
    private static readonly (string Norma, string Descricao, bool Todas, string? Palavras)[] Regras =
    {
        ("NR-06", "Uso de EPI", true, null),
        ("NR-18", "Integração de segurança no canteiro", true, null),
        ("NR-35", "Trabalho em Altura", false, "altura, queda de nivel, queda de pessoa, andaime, telhado, cobertura, fachada, balancim, cadeira suspensa, laje, periferia"),
        ("NR-10", "Segurança em Eletricidade", false, "choque eletrico, eletricidade, eletrico, energizad, quadro de distribuicao, instalacao eletrica, arco eletrico"),
        ("NR-12", "Máquinas e Equipamentos", false, "serra circular, policorte, betoneira, dobradeira, maquina, esmerilhadeira, lixadeira, furadeira, martelete, vibrador de concreto"),
        ("NR-33", "Espaço Confinado", false, "espaco confinado, reservatorio, caixa d'agua, poco, galeria, tanque, cisterna, fossa"),
        ("NR-11", "Movimentação de Cargas", false, "icamento, grua, guindaste, munck, empilhadeira, elevador de carga, guincho, ponte rolante"),
    };

    public static async Task ExecutarAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SstDbContext>();
        if (await db.RegrasCursoObrigatorio.IgnoreQueryFilters().AnyAsync(ct)) return;

        foreach (var r in Regras)
            db.RegrasCursoObrigatorio.Add(new RegraCursoObrigatorio
            {
                Norma = r.Norma,
                Descricao = r.Descricao,
                AplicaTodasFuncoes = r.Todas,
                PalavrasChave = r.Palavras,
            });
        await db.SaveChangesAsync(ct);
    }
}

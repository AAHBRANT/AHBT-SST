using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Seed;

// Carga inicial das regras risco → NR → curso (proposta do sistema; o QSMS valida e edita na tela).
// 07/11/2026: palavras afinadas para cortar falso positivo ("laje", "maquina", "tanque", "poco"),
// regra por perigo padronizado do PGR e NR-20. Regra que o QSMS já mexeu nunca é sobrescrita: só é
// atualizada a que ainda tem exatamente o texto antigo do sistema.
public static class RegrasCursoObrigatorioSeeder
{
    private static readonly (string Norma, string Descricao, bool Todas, string? Palavras, string? Perigos, string? PalavrasAntigas)[] Regras =
    {
        ("NR-06", "Uso de EPI", true, null, null, null),
        ("NR-18", "Integração de segurança no canteiro", true, null, null, null),
        ("NR-35", "Trabalho em Altura", false,
            "trabalho em altura, queda de nivel, queda de pessoa, andaime, telhado, cobertura, fachada, balancim, cadeira suspensa, borda de laje, periferia, escada marinheiro",
            "Queda de altura",
            "altura, queda de nivel, queda de pessoa, andaime, telhado, cobertura, fachada, balancim, cadeira suspensa, laje, periferia"),
        ("NR-10", "Segurança em Eletricidade", false,
            "choque eletrico, eletricidade, energizad, quadro de distribuicao, instalacao eletrica, arco eletrico",
            "Choque elétrico",
            "choque eletrico, eletricidade, eletrico, energizad, quadro de distribuicao, instalacao eletrica, arco eletrico"),
        ("NR-12", "Máquinas e Equipamentos", false,
            "serra circular, policorte, betoneira, dobradeira, operacao de maquina, esmerilhadeira, lixadeira, furadeira, martelete, vibrador de concreto",
            null,
            "serra circular, policorte, betoneira, dobradeira, maquina, esmerilhadeira, lixadeira, furadeira, martelete, vibrador de concreto"),
        ("NR-33", "Espaço Confinado", false,
            "espaco confinado, reservatorio, caixa d'agua, cisterna, fossa, galeria subterranea, poco de visita, tanque enterrado",
            null,
            "espaco confinado, reservatorio, caixa d'agua, poco, galeria, tanque, cisterna, fossa"),
        ("NR-11", "Movimentação de Cargas", false,
            "icamento, grua, guindaste, munck, empilhadeira, elevador de carga, guincho, ponte rolante",
            "Movimentação e içamento de cargas",
            "icamento, grua, guindaste, munck, empilhadeira, elevador de carga, guincho, ponte rolante"),
        ("NR-20", "Inflamáveis e Combustíveis", false,
            "inflamavel, combustivel, abastecimento, diesel, gasolina, glp, tanque de combustivel, armazenamento de gas",
            null,
            null),
    };

    public static async Task ExecutarAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SstDbContext>();
        var existentes = await db.RegrasCursoObrigatorio.IgnoreQueryFilters().ToListAsync(ct);

        foreach (var r in Regras)
        {
            var mesmas = existentes.Where(e => string.Equals(e.Norma, r.Norma, StringComparison.OrdinalIgnoreCase)).ToList();
            if (mesmas.Count == 0)
            {
                db.RegrasCursoObrigatorio.Add(new RegraCursoObrigatorio
                {
                    Norma = r.Norma,
                    Descricao = r.Descricao,
                    AplicaTodasFuncoes = r.Todas,
                    PalavrasChave = r.Palavras,
                    Perigos = r.Perigos,
                });
                continue;
            }

            // Atualiza só a regra intocada (texto antigo do sistema e sem perigo definido).
            foreach (var e in mesmas.Where(e => r.PalavrasAntigas is not null && e.Perigos is null && e.PalavrasChave == r.PalavrasAntigas))
            {
                e.PalavrasChave = r.Palavras;
                e.Perigos = r.Perigos;
            }
        }
        // Curso NR-20 no catálogo (07/11/2026, pedido do usuário): sem ele a regra NR-20 não gera sugestão.
        // Carga horária e validade são proposta do sistema (curso Básico) — o QSMS confere e ajusta no catálogo.
        var cursos = await db.CursosTreinamento.IgnoreQueryFilters().Select(c => c.NormaReferencia).ToListAsync(ct);
        if (!cursos.Any(n => n != null && System.Text.RegularExpressions.Regex.IsMatch(n, @"NR\s*-?\s*0*20\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)))
            db.CursosTreinamento.Add(new CursoTreinamento
            {
                Nome = "NR-20 Segurança e Saúde no Trabalho com Inflamáveis e Combustíveis (Básico)",
                NormaReferencia = "NR-20",
                CargaHorariaMinima = 8,
                ValidadeEmMeses = 36,
            });

        await db.SaveChangesAsync(ct);
    }
}

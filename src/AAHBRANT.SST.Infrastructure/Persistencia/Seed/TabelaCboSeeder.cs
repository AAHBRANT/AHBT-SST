using System.Text;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Seed;

// Carga da tabela oficial da CBO 2002 (Assets/Cbo, convertidos para UTF-8 a partir do "Estrutura CBO (CSV)" do portal
// cbo.mte.gov.br, baixado em 29/10/2026). A tabela é só leitura no app: quando o arquivo muda (nova versão da CBO),
// a contagem deixa de bater e a tabela é recarregada inteira.
public static class TabelaCboSeeder
{
    private const string RecursoOcupacao = "AAHBRANT.SST.Infrastructure.Persistencia.Seed.Assets.Cbo.cbo2002-ocupacao.csv";
    private const string RecursoSinonimo = "AAHBRANT.SST.Infrastructure.Persistencia.Seed.Assets.Cbo.cbo2002-sinonimo.csv";

    public static async Task ExecutarAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SstDbContext>();
        await CarregarAsync(db, LerTitulos(), ct);
    }

    public static List<TituloCbo> LerTitulos() =>
        Ler(RecursoOcupacao, oficial: true).Concat(Ler(RecursoSinonimo, oficial: false)).ToList();

    public static async Task<int> CarregarAsync(SstDbContext db, IReadOnlyList<TituloCbo> titulos, CancellationToken ct = default)
    {
        if (await db.TitulosCbo.IgnoreQueryFilters().CountAsync(ct) == titulos.Count) return 0;

        if (db.Database.IsRelational())
            await db.TitulosCbo.IgnoreQueryFilters().ExecuteDeleteAsync(ct);
        else
            db.TitulosCbo.RemoveRange(await db.TitulosCbo.IgnoreQueryFilters().ToListAsync(ct));
        db.TitulosCbo.AddRange(titulos);
        await db.SaveChangesAsync(ct);
        return titulos.Count;
    }

    private static IEnumerable<TituloCbo> Ler(string recurso, bool oficial)
    {
        using var stream = typeof(TabelaCboSeeder).Assembly.GetManifestResourceStream(recurso)
            ?? throw new InvalidOperationException($"Recurso embutido {recurso} não encontrado.");
        using var leitor = new StreamReader(stream, Encoding.UTF8);
        var lista = new List<TituloCbo>();
        leitor.ReadLine(); // cabeçalho CODIGO;TITULO
        while (leitor.ReadLine() is { } linha)
        {
            var partes = linha.Split(';', 2);
            if (partes.Length < 2 || partes[0].Trim().Length != 6 || string.IsNullOrWhiteSpace(partes[1])) continue;
            lista.Add(new TituloCbo { Codigo = partes[0].Trim(), Titulo = partes[1].Trim(), Oficial = oficial });
        }
        return lista;
    }
}

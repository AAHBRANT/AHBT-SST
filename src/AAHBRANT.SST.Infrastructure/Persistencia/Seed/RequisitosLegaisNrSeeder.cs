using System.Globalization;
using System.Text;
using System.Text.Json;
using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Seed;

// Carga inicial dos requisitos legais a partir do texto oficial das NRs (opção 2 aprovada pelo
// usuário em 08/10/2026). Os itens (Assets/RequisitosLegais/requisitos-nr.json) foram extraídos dos
// PDFs consolidados do gov.br: a descrição é o texto literal do item; título e categoria são
// resumo nosso. Tudo entra como EmRevisao — QSMS lê, ajusta e ativa pela tela; só então o
// requisito passa a valer para o plano de ação da IA.
//
// Idempotente por Norma + Artigo, contando os excluídos (IgnoreQueryFilters): o que QSMS editou ou
// excluiu nunca volta. Na inserção, liga o requisito aos perigos do catálogo cujo nome contém uma
// das palavras-chave do item (ex.: "altura" → NR-35) — sugestão de critério, revisada junto.
public static class RequisitosLegaisNrSeeder
{
    private const string Recurso = "AAHBRANT.SST.Infrastructure.Persistencia.Seed.Assets.RequisitosLegais.requisitos-nr.json";

    public sealed record ItemNr(
        string Norma,
        string Artigo,
        CategoriaRequisitoLegal Categoria,
        string Titulo,
        string Descricao,
        string Fonte,
        List<string> PalavrasChavePerigo);

    public static async Task ExecutarAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SstDbContext>();
        await CarregarAsync(db, LerItens(), ct);
    }

    public static IReadOnlyList<ItemNr> LerItens()
    {
        using var stream = typeof(RequisitosLegaisNrSeeder).Assembly.GetManifestResourceStream(Recurso)
            ?? throw new InvalidOperationException($"Recurso embutido {Recurso} não encontrado.");
        return JsonSerializer.Deserialize<List<ItemNr>>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? new List<ItemNr>();
    }

    public static async Task<int> CarregarAsync(SstDbContext db, IReadOnlyList<ItemNr> itens, CancellationToken ct = default)
    {
        var existentes = (await db.RequisitosLegais
                .IgnoreQueryFilters()
                .Select(r => new { r.Norma, r.Artigo })
                .ToListAsync(ct))
            .Select(r => Chave(r.Norma, r.Artigo))
            .ToHashSet();

        var perigos = (await db.Perigos.Select(p => new { p.Id, p.Nome }).ToListAsync(ct))
            .Select(p => (p.Id, Nome: Normalizar(p.Nome)))
            .ToList();

        var inseridos = 0;
        foreach (var item in itens)
        {
            if (!existentes.Add(Chave(item.Norma, item.Artigo))) continue;

            var requisito = new RequisitoLegal
            {
                Norma = item.Norma,
                Artigo = item.Artigo,
                Titulo = item.Titulo,
                Descricao = item.Descricao,
                Categoria = item.Categoria,
                Status = StatusRequisitoLegal.EmRevisao,
                Fonte = item.Fonte,
                Origem = OrigemRegistro.Importacao,
            };

            var chaves = item.PalavrasChavePerigo.Select(Normalizar).ToList();
            foreach (var perigo in perigos.Where(p => chaves.Any(c => p.Nome.Contains(c))))
            {
                requisito.Criterios.Add(new RequisitoLegalCriterio
                {
                    Tipo = TipoCriterioAplicabilidade.Perigo,
                    PerigoId = perigo.Id,
                    Origem = OrigemRegistro.Importacao,
                });
            }

            db.RequisitosLegais.Add(requisito);
            inseridos++;
        }

        if (inseridos > 0)
            await db.SaveChangesAsync(ct);
        return inseridos;
    }

    private static string Chave(string norma, string? artigo) => $"{norma.Trim().ToUpperInvariant()}|{artigo?.Trim()}";

    // Sem acento e minúsculo: "Elétrico" casa com a palavra-chave "eletric".
    private static string Normalizar(string texto)
    {
        var decomposto = texto.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposto.Length);
        foreach (var c in decomposto)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString();
    }
}

using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Seed;

// Seeder idempotente (mesmo padrão do ChecklistAlojamentoSeeder) que cadastra os 4 materiais de
// apoio já existentes na pasta SGI/05.SST/03.Alojamento — pedido do usuário em 2026-09-09. Os
// arquivos vêm embutidos no assembly (mesmo mecanismo já usado pela logo da AAHBRANT em
// CabecalhoDocumentoPadrao), não de um caminho de disco: essa pasta só existe na máquina de
// desenvolvimento, não vai existir no App Service em produção.
// Só insere um material se NENHUM registro com o mesmo Nome existir ainda: se o usuário já editou
// ou excluiu um desses pela tela, este seeder não insere ele de volta.
public static class MateriaisApoioSeeder
{
    private static readonly (string Nome, string Categoria, string NomeArquivo, string ContentType, string RecursoEmbutido)[] Materiais =
    {
        (
            "Regras da Casa",
            "Sinalização - Alojamento",
            "regras-da-casa.jpeg",
            "image/jpeg",
            "AAHBRANT.SST.Infrastructure.Persistencia.Seed.Assets.MateriaisApoio.regras-da-casa.jpeg"
        ),
        (
            "Higiene (banheiro)",
            "Sinalização - Alojamento",
            "higiene.jpeg",
            "image/jpeg",
            "AAHBRANT.SST.Infrastructure.Persistencia.Seed.Assets.MateriaisApoio.higiene.jpeg"
        ),
        (
            "Instrução Técnica - Alojamento",
            "Instruções Técnicas",
            "instrucao-tecnica-alojamento.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "AAHBRANT.SST.Infrastructure.Persistencia.Seed.Assets.MateriaisApoio.instrucao-tecnica-alojamento.docx"
        ),
        (
            "Regras da Casa (A4 para impressão)",
            "Sinalização - Alojamento",
            "regras-da-casa-a4.pdf",
            "application/pdf",
            "AAHBRANT.SST.Infrastructure.Persistencia.Seed.Assets.MateriaisApoio.regras-da-casa-a4.pdf"
        ),
    };

    public static async Task ExecutarAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SstDbContext>();

        var nomesExistentes = await db.MateriaisApoio
            .IgnoreQueryFilters()
            .Select(m => m.Nome)
            .ToListAsync(ct);

        foreach (var (nome, categoria, nomeArquivo, contentType, recurso) in Materiais)
        {
            if (nomesExistentes.Contains(nome)) continue;

            db.MateriaisApoio.Add(new MaterialApoio
            {
                Nome = nome,
                Categoria = categoria,
                NomeArquivo = nomeArquivo,
                ContentType = contentType,
                Conteudo = CarregarRecursoEmbutido(recurso),
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private static byte[] CarregarRecursoEmbutido(string nomeRecurso)
    {
        var assembly = typeof(MateriaisApoioSeeder).Assembly;
        using var stream = assembly.GetManifestResourceStream(nomeRecurso)
            ?? throw new InvalidOperationException($"Recurso embutido '{nomeRecurso}' não encontrado.");
        using var memoria = new MemoryStream();
        stream.CopyTo(memoria);
        return memoria.ToArray();
    }
}

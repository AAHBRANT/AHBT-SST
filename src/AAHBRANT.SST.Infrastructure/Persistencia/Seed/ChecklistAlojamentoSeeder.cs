using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Seed;

// Seeder idempotente (mesmo padrão do RegraAlertaSeeder) que cadastra o Checklist de Alojamento
// v1 na primeira vez que a API sobe. Itens e seções são transcrição literal de "Check list
// Alojamento.xlsx" (SGI/05.SST/03.Alojamento), que por sua vez operacionaliza a "Instrução
// Técnica Alojamento.docx" da AAHBRANT (NR-18 18.5 / NR-24) — pedido do usuário em 2026-09-09.
// A planilha só tem colunas "Atende"/"Observações", sem indicar quais itens exigem evidência
// fotográfica — por isso ExigeFotografia/ExigeResponsavel/ExigePrazo nascem todos false aqui
// (nenhum dado inventado); o time de SST ajusta pela tela de Checklists caso queira exigir foto
// em itens específicos.
// Só roda se NENHUM ChecklistModelo de TipoInspecao.Alojamento existir ainda: se o usuário já
// criou uma nova versão ou excluiu o modelo pela tela, este seeder não insere nada de volta.
public static class ChecklistAlojamentoSeeder
{
    private const string NomeChecklist = "Checklist de Alojamento";

    private static readonly (string Secao, string Descricao)[] Itens =
    {
        ("Estrutura mínima do alojamento", "Construído com materiais resistentes e protegidos contra intempéries"),
        ("Estrutura mínima do alojamento", "Localizado fora da área de trabalho e distante de fontes de ruído, calor, poeira ou substâncias nocivas"),
        ("Estrutura mínima do alojamento", "Piso resistente, lavável e impermeável"),
        ("Estrutura mínima do alojamento", "Cobertura que evita goteiras e infiltrações"),
        ("Estrutura mínima do alojamento", "Ventilação natural ou mecânica adequada"),
        ("Estrutura mínima do alojamento", "Iluminação natural e/ou artificial suficiente"),

        ("Dormitórios", "Máximo de 6 trabalhadores por dormitório (3m² por pessoa)"),
        ("Dormitórios", "Cama individual para cada trabalhador"),
        ("Dormitórios", "Armário individual com chave ou dispositivo de segurança"),
        ("Dormitórios", "Fornecimento de roupa de cama"),

        ("Instalações sanitárias", "1 vaso sanitário para cada 10 trabalhadores"),
        ("Instalações sanitárias", "1 lavatório para cada 10 trabalhadores"),
        ("Instalações sanitárias", "1 chuveiro com água aquecida para cada 10 trabalhadores"),
        ("Instalações sanitárias", "Vasos sanitários com tampas e assentos"),
        ("Instalações sanitárias", "Piso de fácil higienização"),
        ("Instalações sanitárias", "Revestimento das paredes com material lavável e resistente"),
        ("Instalações sanitárias", "Disponibilidade de saboneteira, papel higiênico, toalhas descartáveis ou secadores de mãos"),

        ("Refeitório", "Refeitório separado dos dormitórios"),
        ("Refeitório", "Ponto de fornecimento de água potável"),
        ("Refeitório", "Lixeira com tampa"),
        ("Refeitório", "Mesas e cadeiras suficientes para todos os funcionários"),
        ("Refeitório", "Itens básicos para refeição (pratos, talheres descartáveis e copos) em quantidade proporcional"),
        ("Refeitório", "Itens básicos de limpeza (detergente, bucha/esponja)"),

        ("Cozinha (quando houver preparo de alimentos)", "Piso e paredes revestidos com material lavável"),
        ("Cozinha (quando houver preparo de alimentos)", "Equipamentos para conservação e preparo dos alimentos (fogão, geladeira)"),
        ("Cozinha (quando houver preparo de alimentos)", "Instalações de gás conforme normas técnicas"),
        ("Cozinha (quando houver preparo de alimentos)", "Filtros de água nas torneiras utilizadas para consumo"),

        ("Lavanderia", "Local específico para lavagem de roupas, com tanques e pias adequados"),
        ("Lavanderia", "Varais ou espaço para secagem das roupas"),
        ("Lavanderia", "Itens de limpeza disponíveis para higienização de fardamentos"),
    };

    public static async Task ExecutarAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SstDbContext>();

        var jaExiste = await db.ChecklistModelos
            .IgnoreQueryFilters()
            .AnyAsync(c => c.TipoInspecao == TipoInspecao.Alojamento, ct);
        if (jaExiste) return;

        var checklist = new ChecklistModelo
        {
            Nome = NomeChecklist,
            TipoInspecao = TipoInspecao.Alojamento,
            Versao = 1,
        };

        var ordem = 1;
        foreach (var (secao, descricao) in Itens)
        {
            checklist.Itens.Add(new ChecklistModeloItem
            {
                Ordem = ordem++,
                Secao = secao,
                Descricao = descricao,
                ExigeFotografia = false,
                ExigeResponsavel = false,
                ExigePrazo = false,
            });
        }

        db.ChecklistModelos.Add(checklist);
        await db.SaveChangesAsync(ct);
    }
}

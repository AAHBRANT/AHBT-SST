using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Persistencia.Seed;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AAHBRANT.SST.Infrastructure.Tests.Seed;

public class ChecklistAlojamentoSeederTests
{
    static ChecklistAlojamentoSeederTests()
    {
        // Mesma chave fixa de teste usada em AAHBRANT.SST.Application.Tests.TestSupport.DbContextFactory —
        // Trabalhador.Cpf tem ValueConverter que precisa dessas chaves configuradas antes de
        // qualquer SaveChanges, mesmo em testes que não tocam Trabalhador.
        CpfCriptografiaContexto.Configurar(
            chaveCriptografia: Enumerable.Repeat((byte)1, 32).ToArray(),
            chaveHash: Enumerable.Repeat((byte)2, 32).ToArray());
    }

    private static ServiceProvider CriarServiceProvider(string nomeBanco)
    {
        var services = new ServiceCollection();
        services.AddDbContext<SstDbContext>(options => options.UseInMemoryDatabase(nomeBanco));
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task ExecutarAsync_BancoVazio_CriaChecklistComTodosOsItensDaPlanilha()
    {
        var provider = CriarServiceProvider(nameof(ExecutarAsync_BancoVazio_CriaChecklistComTodosOsItensDaPlanilha));

        await ChecklistAlojamentoSeeder.ExecutarAsync(provider);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SstDbContext>();
        var checklist = await db.ChecklistModelos.SingleAsync(c => c.TipoInspecao == TipoInspecao.Alojamento);
        var itens = await db.ChecklistModeloItens.Where(i => i.ChecklistModeloId == checklist.Id).ToListAsync();

        Assert.Equal(30, itens.Count);
        Assert.Contains(itens, i => i.Secao == "Dormitórios" && i.Descricao == "Cama individual para cada trabalhador");
        Assert.All(itens, i => Assert.False(string.IsNullOrWhiteSpace(i.Secao)));
    }

    [Fact]
    public async Task ExecutarAsync_ChamadoDuasVezes_NaoDuplicaOChecklist()
    {
        var provider = CriarServiceProvider(nameof(ExecutarAsync_ChamadoDuasVezes_NaoDuplicaOChecklist));

        await ChecklistAlojamentoSeeder.ExecutarAsync(provider);
        await ChecklistAlojamentoSeeder.ExecutarAsync(provider);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SstDbContext>();
        var quantidade = await db.ChecklistModelos.CountAsync(c => c.TipoInspecao == TipoInspecao.Alojamento);

        Assert.Equal(1, quantidade);
    }

    [Fact]
    public async Task ExecutarAsync_UsuarioJaExcluiuOChecklistAnterior_NaoRecriaAutomaticamente()
    {
        var provider = CriarServiceProvider(nameof(ExecutarAsync_UsuarioJaExcluiuOChecklistAnterior_NaoRecriaAutomaticamente));

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SstDbContext>();
            db.ChecklistModelos.Add(new ChecklistModelo
            {
                Nome = "Checklist de Alojamento",
                TipoInspecao = TipoInspecao.Alojamento,
                Versao = 1,
                Ativo = false, // usuário excluiu (soft delete) pela tela
            });
            await db.SaveChangesAsync();
        }

        await ChecklistAlojamentoSeeder.ExecutarAsync(provider);

        using var scopeVerificacao = provider.CreateScope();
        var dbVerificacao = scopeVerificacao.ServiceProvider.GetRequiredService<SstDbContext>();
        var quantidade = await dbVerificacao.ChecklistModelos
            .IgnoreQueryFilters()
            .CountAsync(c => c.TipoInspecao == TipoInspecao.Alojamento);

        Assert.Equal(1, quantidade);
    }
}

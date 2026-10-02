using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Persistencia.Seed;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AAHBRANT.SST.Infrastructure.Tests.Seed;

public class ChecklistVeiculoSeederTests
{
    static ChecklistVeiculoSeederTests()
    {
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
    public async Task ExecutarAsync_BancoVazio_CriaUmChecklistPorTipoComOsItensDaPlanilha()
    {
        var provider = CriarServiceProvider(nameof(ExecutarAsync_BancoVazio_CriaUmChecklistPorTipoComOsItensDaPlanilha));

        await ChecklistVeiculoSeeder.ExecutarAsync(provider);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SstDbContext>();
        var checklists = await db.ChecklistModelos.Include(c => c.Itens)
            .Where(c => c.TipoInspecao == TipoInspecao.Veiculo).ToListAsync();

        Assert.Equal(5, checklists.Count);
        Assert.Equal(14, checklists.Single(c => c.TipoVeiculo == TipoVeiculo.CaminhaoBasculante).Itens.Count);
        Assert.Equal(14, checklists.Single(c => c.TipoVeiculo == TipoVeiculo.Retroescavadeira).Itens.Count);
        Assert.Equal(14, checklists.Single(c => c.TipoVeiculo == TipoVeiculo.EscavadeiraHidraulica).Itens.Count);
        Assert.Equal(17, checklists.Single(c => c.TipoVeiculo == TipoVeiculo.CaminhaoCarroceria).Itens.Count);
        Assert.Equal(16, checklists.Single(c => c.TipoVeiculo == TipoVeiculo.CaminhaoMunck).Itens.Count);
        // Foto é regra por tipo de inspeção (só em NC), nunca por flag de item.
        Assert.All(checklists.SelectMany(c => c.Itens), i => Assert.False(i.ExigeFotografia));
    }

    [Fact]
    public async Task ExecutarAsync_RodadoDuasVezes_NaoDuplica()
    {
        var provider = CriarServiceProvider(nameof(ExecutarAsync_RodadoDuasVezes_NaoDuplica));

        await ChecklistVeiculoSeeder.ExecutarAsync(provider);
        await ChecklistVeiculoSeeder.ExecutarAsync(provider);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SstDbContext>();
        Assert.Equal(5, await db.ChecklistModelos.CountAsync(c => c.TipoInspecao == TipoInspecao.Veiculo));
    }
}

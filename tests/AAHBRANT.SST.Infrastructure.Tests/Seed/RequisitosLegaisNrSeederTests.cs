using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Persistencia.Seed;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AAHBRANT.SST.Infrastructure.Tests.Seed;

public class RequisitosLegaisNrSeederTests
{
    static RequisitosLegaisNrSeederTests()
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
    public void ArquivoEmbutido_TemItensValidosDentroDosLimitesDaTabela()
    {
        var itens = RequisitosLegaisNrSeeder.LerItens();

        Assert.True(itens.Count >= 50);
        Assert.Equal(itens.Count, itens.Select(i => $"{i.Norma}|{i.Artigo}").Distinct().Count());
        Assert.All(itens, i =>
        {
            Assert.Matches(@"^NR-\d{2}$", i.Norma);
            Assert.StartsWith(i.Artigo, i.Descricao); // descrição é o texto literal do próprio item
            Assert.InRange(i.Descricao.Length, 1, 2000);
            Assert.InRange(i.Titulo.Length, 1, 300);
            Assert.InRange(i.Fonte.Length, 1, 500);
            Assert.Contains("gov.br", i.Fonte);
            Assert.True(Enum.IsDefined(i.Categoria));
        });
    }

    [Fact]
    public async Task ExecutarAsync_EntraEmRevisaoELigaPerigosPorPalavraChave()
    {
        var provider = CriarServiceProvider(nameof(ExecutarAsync_EntraEmRevisaoELigaPerigosPorPalavraChave));
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SstDbContext>();
            db.Perigos.AddRange(new Perigo { Nome = "Trabalho em altura" }, new Perigo { Nome = "Choque Elétrico" }, new Perigo { Nome = "Poeira" });
            await db.SaveChangesAsync();
        }

        await RequisitosLegaisNrSeeder.ExecutarAsync(provider);

        using var verificar = provider.CreateScope();
        var banco = verificar.ServiceProvider.GetRequiredService<SstDbContext>();
        var requisitos = await banco.RequisitosLegais.Include(r => r.Criterios).ThenInclude(c => c.Perigo).ToListAsync();

        Assert.Equal(RequisitosLegaisNrSeeder.LerItens().Count, requisitos.Count);
        Assert.All(requisitos, r =>
        {
            Assert.Equal(StatusRequisitoLegal.EmRevisao, r.Status);
            Assert.Equal(OrigemRegistro.Importacao, r.Origem);
        });
        var nr35 = requisitos.Single(r => r.Norma == "NR-35" && r.Artigo == "35.5.5");
        Assert.Equal(new[] { "Trabalho em altura" }, nr35.Criterios.Select(c => c.Perigo!.Nome));
        var nr10 = requisitos.Single(r => r.Norma == "NR-10" && r.Artigo == "10.8.8");
        Assert.Equal(new[] { "Choque Elétrico" }, nr10.Criterios.Select(c => c.Perigo!.Nome)); // sem acento casa
        Assert.Empty(requisitos.Single(r => r.Norma == "NR-01" && r.Artigo == "1.7.1").Criterios);
    }

    [Fact]
    public async Task ExecutarAsync_NaoDuplicaNemRecriaOQueQsmsEditouOuExcluiu()
    {
        var provider = CriarServiceProvider(nameof(ExecutarAsync_NaoDuplicaNemRecriaOQueQsmsEditouOuExcluiu));
        await RequisitosLegaisNrSeeder.ExecutarAsync(provider);

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SstDbContext>();
            var excluido = await db.RequisitosLegais.SingleAsync(r => r.Artigo == "6.3");
            excluido.Ativo = false; // exclusão lógica pela tela
            var editado = await db.RequisitosLegais.SingleAsync(r => r.Artigo == "35.6.1");
            editado.Status = StatusRequisitoLegal.Ativo;
            editado.Titulo = "Título ajustado pelo QSMS";
            await db.SaveChangesAsync();
        }

        await RequisitosLegaisNrSeeder.ExecutarAsync(provider);

        using var verificar = provider.CreateScope();
        var banco = verificar.ServiceProvider.GetRequiredService<SstDbContext>();
        Assert.Equal(RequisitosLegaisNrSeeder.LerItens().Count, await banco.RequisitosLegais.IgnoreQueryFilters().CountAsync());
        Assert.False(await banco.RequisitosLegais.AnyAsync(r => r.Artigo == "6.3"));
        var editadoDepois = await banco.RequisitosLegais.SingleAsync(r => r.Artigo == "35.6.1");
        Assert.Equal("Título ajustado pelo QSMS", editadoDepois.Titulo);
        Assert.Equal(StatusRequisitoLegal.Ativo, editadoDepois.Status);
    }
}

using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Persistencia.Seed;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AAHBRANT.SST.Infrastructure.Tests.Seed;

public class MateriaisApoioSeederTests
{
    static MateriaisApoioSeederTests()
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
    public async Task ExecutarAsync_BancoVazio_CarregaOsQuatroMateriaisComConteudoReal()
    {
        var provider = CriarServiceProvider(nameof(ExecutarAsync_BancoVazio_CarregaOsQuatroMateriaisComConteudoReal));

        await MateriaisApoioSeeder.ExecutarAsync(provider);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SstDbContext>();
        var materiais = await db.MateriaisApoio.ToListAsync();

        Assert.Equal(4, materiais.Count);
        // Cada arquivo embutido tem tamanho conhecido e diferente de zero — confirma que o recurso
        // embutido foi de fato lido (GetManifestResourceStream não retornou null/vazio).
        Assert.All(materiais, m => Assert.True(m.Conteudo.Length > 0));
        Assert.Contains(materiais, m => m.Nome == "Regras da Casa" && m.ContentType == "image/jpeg");
        Assert.Contains(materiais, m => m.Nome == "Instrução Técnica - Alojamento"
            && m.ContentType == "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        Assert.Contains(materiais, m => m.Nome == "Regras da Casa (A4 para impressão)" && m.ContentType == "application/pdf");
    }

    [Fact]
    public async Task ExecutarAsync_ChamadoDuasVezes_NaoDuplicaOsMateriais()
    {
        var provider = CriarServiceProvider(nameof(ExecutarAsync_ChamadoDuasVezes_NaoDuplicaOsMateriais));

        await MateriaisApoioSeeder.ExecutarAsync(provider);
        await MateriaisApoioSeeder.ExecutarAsync(provider);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SstDbContext>();
        var quantidade = await db.MateriaisApoio.CountAsync();

        Assert.Equal(4, quantidade);
    }

    [Fact]
    public async Task ExecutarAsync_UsuarioJaExcluiuUmMaterial_NaoRecriaAqueleEspecifico()
    {
        var provider = CriarServiceProvider(nameof(ExecutarAsync_UsuarioJaExcluiuUmMaterial_NaoRecriaAqueleEspecifico));

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SstDbContext>();
            db.MateriaisApoio.Add(new MaterialApoio
            {
                Nome = "Regras da Casa",
                ContentType = "image/jpeg",
                NomeArquivo = "x.jpeg",
                Conteudo = new byte[] { 1 },
                Ativo = false, // usuário excluiu (soft delete) pela tela
            });
            await db.SaveChangesAsync();
        }

        await MateriaisApoioSeeder.ExecutarAsync(provider);

        using var scopeVerificacao = provider.CreateScope();
        var dbVerificacao = scopeVerificacao.ServiceProvider.GetRequiredService<SstDbContext>();
        var quantidadeComEsseNome = await dbVerificacao.MateriaisApoio
            .IgnoreQueryFilters()
            .CountAsync(m => m.Nome == "Regras da Casa");
        var quantidadeTotal = await dbVerificacao.MateriaisApoio.IgnoreQueryFilters().CountAsync();

        Assert.Equal(1, quantidadeComEsseNome); // não recriou
        Assert.Equal(4, quantidadeTotal); // os outros 3 (que não existiam) foram criados normalmente
    }
}

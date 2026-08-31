using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.TestSupport;

// Dublê de IAppDbContext para os testes: SstDbContext de verdade, banco InMemory do EF Core em
// vez de SQL Server. Cada chamada cria um banco isolado (Guid novo) para não vazar estado entre
// testes. CurrentUserService no modo padrão (acesso global) — os filtros de escopo por obra
// (RBAC) não são o alvo destes testes, então ficam neutros de propósito.
public static class DbContextFactory
{
    static DbContextFactory()
    {
        // Trabalhador.Cpf é criptografado por um ValueConverter que lê chaves estáticas
        // (CpfCriptografiaContexto — carregadas normalmente em AddInfrastructure a partir de
        // appsettings, que os testes não executam). Sem isto, qualquer SaveChanges envolvendo
        // Trabalhador lança InvalidOperationException. Chave fixa e óbvia de teste — nunca usar
        // fora deste projeto.
        CpfCriptografiaContexto.Configurar(
            chaveCriptografia: Enumerable.Repeat((byte)1, 32).ToArray(),
            chaveHash: Enumerable.Repeat((byte)2, 32).ToArray());
    }

    public static SstDbContext Criar()
    {
        var opcoes = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new SstDbContext(opcoes, new CurrentUserService());
    }
}

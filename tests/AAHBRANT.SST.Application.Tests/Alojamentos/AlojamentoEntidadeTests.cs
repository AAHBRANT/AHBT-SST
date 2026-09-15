using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Alojamentos;

public class AlojamentoEntidadeTests
{
    private static SstDbContext CriarDbSqlite(string nomeArquivo)
    {
        // InMemory do EF Core não aplica índices/constraints reais — para testar um índice único
        // filtrado é preciso um provider relacional de verdade. Sqlite in-memory é o mais leve.
        var conexao = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        conexao.Open();
        var options = new DbContextOptionsBuilder<SstDbContext>().UseSqlite(conexao).Options;
        var db = new SstDbContext(options, new CurrentUserService());
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task SaveChanges_DoisVinculosAtivosParaMesmoTrabalhador_LancaExcecaoDeConstraint()
    {
        using var db = CriarDbSqlite(nameof(SaveChanges_DoisVinculosAtivosParaMesmoTrabalhador_LancaExcecaoDeConstraint));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        var funcao = new Funcao { Nome = "Pedreiro" };
        var trabalhador = new Trabalhador { Nome = "Fulano", ObraId = obra.Id, FuncaoId = funcao.Id };
        var alojamentoA = new Alojamento { Nome = "Alojamento A", ObraId = obra.Id };
        var alojamentoB = new Alojamento { Nome = "Alojamento B", ObraId = obra.Id };
        db.Obras.Add(obra);
        db.Funcoes.Add(funcao);
        db.Trabalhadores.Add(trabalhador);
        db.Alojamentos.AddRange(alojamentoA, alojamentoB);
        await db.SaveChangesAsync();

        db.AlojamentoMoradores.Add(new AlojamentoMorador { AlojamentoId = alojamentoA.Id, TrabalhadorId = trabalhador.Id, DataDesde = DateTime.UtcNow });
        await db.SaveChangesAsync();

        db.AlojamentoMoradores.Add(new AlojamentoMorador { AlojamentoId = alojamentoB.Id, TrabalhadorId = trabalhador.Id, DataDesde = DateTime.UtcNow });

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}

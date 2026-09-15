using AAHBRANT.SST.Application.Alojamentos.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Alojamentos;

public class ListarAlojamentosQueryHandlerTests
{
    private static SstDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nomeBanco).Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_SemInspecaoNenhuma_StatusNunca()
    {
        var db = CriarDb(nameof(Handle_SemInspecaoNenhuma_StatusNunca));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        db.Obras.Add(obra);
        db.ConfiguracoesAlojamento.Add(new ConfiguracaoAlojamento { DiasParaInspecaoAtrasada = 30 });
        var alojamento = new Alojamento { Nome = "Alojamento 01", ObraId = obra.Id };
        db.Alojamentos.Add(alojamento);
        await db.SaveChangesAsync();

        var handler = new ListarAlojamentosQueryHandler(db);
        var resultado = await handler.Handle(new ListarAlojamentosQuery(obra.Id), default);

        var dto = Assert.Single(resultado);
        Assert.Equal("nunca", dto.StatusUltimaInspecao);
        Assert.Equal(0, dto.TotalMoradores);
    }
}

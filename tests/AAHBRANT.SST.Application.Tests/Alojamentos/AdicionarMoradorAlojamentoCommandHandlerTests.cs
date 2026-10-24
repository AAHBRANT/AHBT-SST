using AAHBRANT.SST.Application.Alojamentos.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Alojamentos;

public class AdicionarMoradorAlojamentoCommandHandlerTests
{
    private static SstDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nomeBanco).Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_TrabalhadorJaTemVinculoAtivo_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Handle_TrabalhadorJaTemVinculoAtivo_LancaInvalidOperationException));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        var trabalhador = new Trabalhador { Nome = "Fulano", ObraId = obra.Id };
        var alojamento = new Alojamento { Nome = "Alojamento 01", ObraId = obra.Id };
        db.AddRange(obra, trabalhador, alojamento);
        await db.SaveChangesAsync();

        var handler = new AdicionarMoradorAlojamentoCommandHandler(db);
        await handler.Handle(new AdicionarMoradorAlojamentoCommand(alojamento.Id, trabalhador.Id), default);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new AdicionarMoradorAlojamentoCommand(alojamento.Id, trabalhador.Id), default));
    }
}

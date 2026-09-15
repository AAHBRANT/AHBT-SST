using AAHBRANT.SST.Application.Alojamentos.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Alojamentos;

public class AtualizarConfiguracaoAlojamentoCommandHandlerTests
{
    [Fact]
    public async Task Handle_ConfiguracaoExistente_AtualizaDias()
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nameof(Handle_ConfiguracaoExistente_AtualizaDias)).Options;
        var db = new SstDbContext(options, new CurrentUserService());
        db.ConfiguracoesAlojamento.Add(new ConfiguracaoAlojamento { DiasParaInspecaoAtrasada = 30 });
        await db.SaveChangesAsync();

        var handler = new AtualizarConfiguracaoAlojamentoCommandHandler(db);
        await handler.Handle(new AtualizarConfiguracaoAlojamentoCommand(45), default);

        var config = await db.ConfiguracoesAlojamento.FirstAsync();
        Assert.Equal(45, config.DiasParaInspecaoAtrasada);
    }
}

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

    [Fact]
    public async Task Handle_ConfiguracaoNaoSeedada_LancaInvalidOperationException()
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nameof(Handle_ConfiguracaoNaoSeedada_LancaInvalidOperationException)).Options;
        var db = new SstDbContext(options, new CurrentUserService());
        // sem adicionar nenhuma ConfiguracaoAlojamento

        var handler = new AtualizarConfiguracaoAlojamentoCommandHandler(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new AtualizarConfiguracaoAlojamentoCommand(45), default));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(365, true)]
    [InlineData(366, false)]
    public void Validator_LimitesDeDias_ValidaCorretamente(int dias, bool esperadoValido)
    {
        var validator = new AtualizarConfiguracaoAlojamentoCommandValidator();
        var resultado = validator.Validate(new AtualizarConfiguracaoAlojamentoCommand(dias));
        Assert.Equal(esperadoValido, resultado.IsValid);
    }
}

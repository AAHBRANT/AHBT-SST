using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.CursosTreinamento.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.CursosTreinamento;

public class AtualizarCursoTreinamentoCommandHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_MarcaComoIntegracaoSeguranca_DesmarcaCursoAnteriorAutomaticamente()
    {
        var db = CriarDb(nameof(Handle_MarcaComoIntegracaoSeguranca_DesmarcaCursoAnteriorAutomaticamente));
        var cursoAntigo = new CursoTreinamento { Nome = "Integração antiga", CargaHorariaMinima = 4, ValidadeEmMeses = 12, EhIntegracaoSeguranca = true };
        var cursoNovo = new CursoTreinamento { Nome = "Integração nova", CargaHorariaMinima = 4, ValidadeEmMeses = 12 };
        db.CursosTreinamento.AddRange(cursoAntigo, cursoNovo);
        await db.SaveChangesAsync();
        var handler = new AtualizarCursoTreinamentoCommandHandler(db);

        await handler.Handle(new AtualizarCursoTreinamentoCommand(
            cursoNovo.Id, cursoNovo.Nome, null, 4, 12, EhIntegracaoSeguranca: true), default);

        Assert.True((await db.CursosTreinamento.FirstAsync(c => c.Id == cursoNovo.Id)).EhIntegracaoSeguranca);
        Assert.False((await db.CursosTreinamento.FirstAsync(c => c.Id == cursoAntigo.Id)).EhIntegracaoSeguranca);
    }

    [Fact]
    public async Task Handle_CursoInexistente_LancaKeyNotFoundException()
    {
        var db = CriarDb(nameof(Handle_CursoInexistente_LancaKeyNotFoundException));
        var handler = new AtualizarCursoTreinamentoCommandHandler(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new AtualizarCursoTreinamentoCommand(Guid.NewGuid(), "X", null, 4, 12), default));
    }
}

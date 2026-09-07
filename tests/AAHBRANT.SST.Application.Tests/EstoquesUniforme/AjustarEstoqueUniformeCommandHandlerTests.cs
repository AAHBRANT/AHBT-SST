using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.EstoquesUniforme.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.EstoquesUniforme;

public class AjustarEstoqueUniformeCommandHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private static async Task<(CatalogoUniforme Peca, Obra Obra)> SemearAsync(IAppDbContext db)
    {
        var peca = new CatalogoUniforme { Nome = "Camisa" };
        var obra = new Obra { Nome = "Obra Teste" };
        db.CatalogoUniformes.Add(peca);
        db.Obras.Add(obra);
        await db.SaveChangesAsync();
        return (peca, obra);
    }

    [Fact]
    public async Task Handle_AjusteParaBaixo_RegistraDeltaNegativo()
    {
        var db = CriarDb(nameof(Handle_AjusteParaBaixo_RegistraDeltaNegativo));
        var (peca, obra) = await SemearAsync(db);
        await new RegistrarEntradaEstoqueUniformeCommandHandler(db)
            .Handle(new RegistrarEntradaEstoqueUniformeCommand(peca.Id, obra.Id, "M", 10, null), default);
        var handler = new AjustarEstoqueUniformeCommandHandler(db);

        await handler.Handle(new AjustarEstoqueUniformeCommand(peca.Id, obra.Id, "M", 6, "Divergência de inventário"), default);

        var estoque = await db.EstoquesUniforme.SingleAsync(e => e.CatalogoUniformeId == peca.Id && e.ObraId == obra.Id && e.Tamanho == "M");
        Assert.Equal(6, estoque.Saldo);
        var movimentacao = await db.MovimentacoesEstoqueUniforme.OrderByDescending(m => m.CreatedAtUtc).FirstAsync(m => m.EstoqueUniformeId == estoque.Id);
        Assert.Equal(-4, movimentacao.Quantidade);
    }

    [Fact]
    public async Task Handle_SaldoIgualAoAtual_NaoRegistraMovimentacao()
    {
        var db = CriarDb(nameof(Handle_SaldoIgualAoAtual_NaoRegistraMovimentacao));
        var (peca, obra) = await SemearAsync(db);
        await new RegistrarEntradaEstoqueUniformeCommandHandler(db)
            .Handle(new RegistrarEntradaEstoqueUniformeCommand(peca.Id, obra.Id, "M", 10, null), default);
        var handler = new AjustarEstoqueUniformeCommandHandler(db);

        await handler.Handle(new AjustarEstoqueUniformeCommand(peca.Id, obra.Id, "M", 10, "Conferência sem divergência"), default);

        var estoque = await db.EstoquesUniforme.SingleAsync(e => e.CatalogoUniformeId == peca.Id && e.ObraId == obra.Id && e.Tamanho == "M");
        var totalMovimentacoes = await db.MovimentacoesEstoqueUniforme.CountAsync(m => m.EstoqueUniformeId == estoque.Id);
        Assert.Equal(1, totalMovimentacoes); // só a entrada manual inicial, o ajuste não gerou uma 2ª linha
    }
}

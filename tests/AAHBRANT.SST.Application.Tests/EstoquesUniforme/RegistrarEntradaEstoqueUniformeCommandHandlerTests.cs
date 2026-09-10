using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.EstoquesUniforme.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.EstoquesUniforme;

public class RegistrarEntradaEstoqueUniformeCommandHandlerTests
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
    public async Task Handle_PrimeiraEntradaDoTamanho_CriaEstoqueComSaldoIgualAQuantidade()
    {
        var db = CriarDb(nameof(Handle_PrimeiraEntradaDoTamanho_CriaEstoqueComSaldoIgualAQuantidade));
        var (peca, obra) = await SemearAsync(db);
        var handler = new RegistrarEntradaEstoqueUniformeCommandHandler(db);

        await handler.Handle(new RegistrarEntradaEstoqueUniformeCommand(peca.Id, obra.Id, "M", 10, "Compra inicial"), default);

        var estoque = await db.EstoquesUniforme.SingleAsync(e => e.CatalogoUniformeId == peca.Id && e.ObraId == obra.Id && e.Tamanho == "M");
        Assert.Equal(10, estoque.Saldo);
        var movimentacao = await db.MovimentacoesEstoqueUniforme.SingleAsync(m => m.EstoqueUniformeId == estoque.Id);
        Assert.Equal(TipoMovimentacaoEstoqueUniforme.EntradaManual, movimentacao.Tipo);
        Assert.Equal(10, movimentacao.SaldoResultante);
    }

    [Fact]
    public async Task Handle_EntradaAdicionalNoMesmoTamanho_SomaAoSaldoExistente()
    {
        var db = CriarDb(nameof(Handle_EntradaAdicionalNoMesmoTamanho_SomaAoSaldoExistente));
        var (peca, obra) = await SemearAsync(db);
        var handler = new RegistrarEntradaEstoqueUniformeCommandHandler(db);
        await handler.Handle(new RegistrarEntradaEstoqueUniformeCommand(peca.Id, obra.Id, "M", 10, null), default);

        await handler.Handle(new RegistrarEntradaEstoqueUniformeCommand(peca.Id, obra.Id, "M", 5, null), default);

        var estoque = await db.EstoquesUniforme.SingleAsync(e => e.CatalogoUniformeId == peca.Id && e.ObraId == obra.Id && e.Tamanho == "M");
        Assert.Equal(15, estoque.Saldo);
    }

    [Fact]
    public async Task Handle_TamanhoDiferenteDaMesmaPeca_CriaBucketSeparado()
    {
        var db = CriarDb(nameof(Handle_TamanhoDiferenteDaMesmaPeca_CriaBucketSeparado));
        var (peca, obra) = await SemearAsync(db);
        var handler = new RegistrarEntradaEstoqueUniformeCommandHandler(db);
        await handler.Handle(new RegistrarEntradaEstoqueUniformeCommand(peca.Id, obra.Id, "M", 10, null), default);

        await handler.Handle(new RegistrarEntradaEstoqueUniformeCommand(peca.Id, obra.Id, "G", 7, null), default);

        var estoques = await db.EstoquesUniforme.Where(e => e.CatalogoUniformeId == peca.Id && e.ObraId == obra.Id).ToListAsync();
        Assert.Equal(2, estoques.Count);
        Assert.Equal(10, estoques.Single(e => e.Tamanho == "M").Saldo);
        Assert.Equal(7, estoques.Single(e => e.Tamanho == "G").Saldo);
    }
}

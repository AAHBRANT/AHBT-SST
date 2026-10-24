using AAHBRANT.SST.Application.EntregasUniforme.Commands;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.EntregasUniforme;

// Entrega de uniforme não tinha exclusão nenhuma até 24/09. Ao criar, o ponto de atenção é o mesmo
// que mordeu no EPI e no EPC: registrar debita o estoque, então excluir precisa devolver — e aqui o
// saldo é por peça + TAMANHO, não só por peça.
public class ExcluirEntregaUniformeCommandHandlerTests
{
    private static async Task<(Trabalhador trabalhador, CatalogoUniforme peca, EstoqueUniforme estoque)> SemearAsync(
        SstDbContext db, string tamanho = "G", int saldoInicial = 5)
    {
        var trabalhador = new Trabalhador { ObraId = Guid.NewGuid(), FuncaoId = Guid.NewGuid(), Nome = "Carlos Eduardo", Cpf = "00000000000" };
        var peca = new CatalogoUniforme { Nome = "Camisa Manga Longa" };
        var estoque = new EstoqueUniforme
        {
            CatalogoUniformeId = peca.Id,
            ObraId = trabalhador.ObraId,
            Tamanho = tamanho,
            Saldo = saldoInicial,
        };
        db.Trabalhadores.Add(trabalhador);
        db.CatalogoUniformes.Add(peca);
        db.EstoquesUniforme.Add(estoque);
        await db.SaveChangesAsync();
        return (trabalhador, peca, estoque);
    }

    private static async Task<EntregaUniforme> SemearEntregaAsync(
        SstDbContext db, Trabalhador trabalhador, CatalogoUniforme peca, string tamanho, int quantidade)
    {
        var entrega = new EntregaUniforme
        {
            TrabalhadorId = trabalhador.Id,
            CatalogoUniformeId = peca.Id,
            Tamanho = tamanho,
            Quantidade = quantidade,
            DataEntrega = new DateTime(2026, 9, 20),
        };
        db.EntregasUniforme.Add(entrega);
        await db.SaveChangesAsync();
        return entrega;
    }

    [Fact]
    public async Task Handle_EntregaExcluida_DevolveQuantidadeAoEstoque()
    {
        var db = DbContextFactory.Criar();
        var (trabalhador, peca, estoque) = await SemearAsync(db, "G", saldoInicial: 3);
        var entrega = await SemearEntregaAsync(db, trabalhador, peca, "G", quantidade: 2);
        var handler = new ExcluirEntregaUniformeCommandHandler(db);

        await handler.Handle(new ExcluirEntregaUniformeCommand(entrega.Id), default);

        Assert.Equal(5, (await db.EstoquesUniforme.SingleAsync(e => e.Id == estoque.Id)).Saldo);
        Assert.Empty(db.EntregasUniforme);
    }

    [Fact]
    public async Task Handle_EntregaExcluida_RegistraMovimentacaoDeEstorno()
    {
        var db = DbContextFactory.Criar();
        var (trabalhador, peca, _) = await SemearAsync(db, "G", saldoInicial: 3);
        var entrega = await SemearEntregaAsync(db, trabalhador, peca, "G", quantidade: 2);
        var handler = new ExcluirEntregaUniformeCommandHandler(db);

        await handler.Handle(new ExcluirEntregaUniformeCommand(entrega.Id), default);

        var movimentacao = Assert.Single(db.MovimentacoesEstoqueUniforme);
        Assert.Equal(TipoMovimentacaoEstoqueUniforme.AjusteManual, movimentacao.Tipo);
        Assert.Equal(2, movimentacao.Quantidade);
        Assert.Equal(5, movimentacao.SaldoResultante);
        Assert.Contains("Estorno", movimentacao.Observacao ?? string.Empty);
    }

    // O saldo é por peça + tamanho: devolver no bucket errado infla um tamanho e deixa o outro
    // furado. Aqui existe estoque de "G" e a entrega era "M" — nada pode ser somado no "G".
    [Fact]
    public async Task Handle_TamanhoSemEstoqueCadastrado_NaoDevolveNoBucketErrado()
    {
        var db = DbContextFactory.Criar();
        var (trabalhador, peca, estoqueG) = await SemearAsync(db, "G", saldoInicial: 3);
        var entrega = await SemearEntregaAsync(db, trabalhador, peca, "M", quantidade: 2);
        var handler = new ExcluirEntregaUniformeCommandHandler(db);

        await handler.Handle(new ExcluirEntregaUniformeCommand(entrega.Id), default);

        Assert.Equal(3, (await db.EstoquesUniforme.SingleAsync(e => e.Id == estoqueG.Id)).Saldo);
        Assert.Empty(db.MovimentacoesEstoqueUniforme);
        Assert.Empty(db.EntregasUniforme);
    }

    [Fact]
    public async Task Handle_EntregaInexistente_LancaKeyNotFound()
    {
        var db = DbContextFactory.Criar();
        var handler = new ExcluirEntregaUniformeCommandHandler(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new ExcluirEntregaUniformeCommand(Guid.NewGuid()), default));
    }
}

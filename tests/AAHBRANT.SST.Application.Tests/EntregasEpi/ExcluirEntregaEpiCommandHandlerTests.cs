using AAHBRANT.SST.Application.EntregasEpi.Commands;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.EntregasEpi;

// Registrar a entrega baixa o estoque; excluir precisa devolver. Antes não devolvia, e cada
// exclusão apagava unidades do saldo da obra em silêncio — defeito que só apareceu quando o
// Administrador ganhou o botão de excluir na tela (23/09) para limpar lançamento de teste.
public class ExcluirEntregaEpiCommandHandlerTests
{
    private static async Task<(Trabalhador trabalhador, CatalogoEpi catalogo, EstoqueEpi estoque)> SemearAsync(
        SstDbContext db, int saldoInicial = 10)
    {
        var trabalhador = new Trabalhador { ObraId = Guid.NewGuid(), FuncaoId = Guid.NewGuid(), Nome = "Carlos Eduardo", Cpf = "00000000000" };
        var catalogo = new CatalogoEpi { Nome = "Capacete de Segurança", VidaUtilEmMeses = 12 };
        var estoque = new EstoqueEpi { CatalogoEpiId = catalogo.Id, ObraId = trabalhador.ObraId, Saldo = saldoInicial };
        db.Trabalhadores.Add(trabalhador);
        db.CatalogoEpis.Add(catalogo);
        db.EstoquesEpi.Add(estoque);
        await db.SaveChangesAsync();
        return (trabalhador, catalogo, estoque);
    }

    private static async Task<EntregaEpi> SemearEntregaAsync(
        SstDbContext db, Trabalhador trabalhador, CatalogoEpi catalogo, int quantidade, int? devolvida = null)
    {
        var entrega = new EntregaEpi
        {
            TrabalhadorId = trabalhador.Id,
            CatalogoEpiId = catalogo.Id,
            DataEntrega = new DateTime(2026, 9, 20),
            Quantidade = quantidade,
            QuantidadeDevolucao = devolvida,
            DataDevolucao = devolvida is null ? null : new DateTime(2026, 9, 22),
        };
        db.EntregasEpi.Add(entrega);
        await db.SaveChangesAsync();
        return entrega;
    }

    [Fact]
    public async Task Handle_EntregaEmPosse_DevolveQuantidadeAoEstoque()
    {
        var db = DbContextFactory.Criar();
        var (trabalhador, catalogo, estoque) = await SemearAsync(db, saldoInicial: 7);
        var entrega = await SemearEntregaAsync(db, trabalhador, catalogo, quantidade: 3);
        var handler = new ExcluirEntregaEpiCommandHandler(db);

        await handler.Handle(new ExcluirEntregaEpiCommand(entrega.Id), default);

        Assert.Equal(10, (await db.EstoquesEpi.SingleAsync(e => e.Id == estoque.Id)).Saldo);
        Assert.Empty(db.EntregasEpi);
    }

    [Fact]
    public async Task Handle_EntregaExcluida_RegistraMovimentacaoDeEstorno()
    {
        var db = DbContextFactory.Criar();
        var (trabalhador, catalogo, _) = await SemearAsync(db, saldoInicial: 7);
        var entrega = await SemearEntregaAsync(db, trabalhador, catalogo, quantidade: 3);
        var handler = new ExcluirEntregaEpiCommandHandler(db);

        await handler.Handle(new ExcluirEntregaEpiCommand(entrega.Id), default);

        var movimentacao = Assert.Single(db.MovimentacoesEstoqueEpi);
        Assert.Equal(TipoMovimentacaoEstoqueEpi.AjusteManual, movimentacao.Tipo);
        Assert.Equal(3, movimentacao.Quantidade);
        Assert.Equal(10, movimentacao.SaldoResultante);
        Assert.Contains("Estorno", movimentacao.Observacao ?? string.Empty);
    }

    // Parte já devolvida voltou ao saldo na devolução; estornar tudo de novo criaria estoque do nada.
    [Fact]
    public async Task Handle_EntregaParcialmenteDevolvida_EstornaSoOQueEstavaEmPosse()
    {
        var db = DbContextFactory.Criar();
        var (trabalhador, catalogo, estoque) = await SemearAsync(db, saldoInicial: 8);
        var entrega = await SemearEntregaAsync(db, trabalhador, catalogo, quantidade: 5, devolvida: 3);
        var handler = new ExcluirEntregaEpiCommandHandler(db);

        await handler.Handle(new ExcluirEntregaEpiCommand(entrega.Id), default);

        Assert.Equal(10, (await db.EstoquesEpi.SingleAsync(e => e.Id == estoque.Id)).Saldo);
    }

    [Fact]
    public async Task Handle_EntregaTotalmenteDevolvida_NaoMexeNoEstoque()
    {
        var db = DbContextFactory.Criar();
        var (trabalhador, catalogo, estoque) = await SemearAsync(db, saldoInicial: 10);
        var entrega = await SemearEntregaAsync(db, trabalhador, catalogo, quantidade: 4, devolvida: 4);
        var handler = new ExcluirEntregaEpiCommandHandler(db);

        await handler.Handle(new ExcluirEntregaEpiCommand(entrega.Id), default);

        Assert.Equal(10, (await db.EstoquesEpi.SingleAsync(e => e.Id == estoque.Id)).Saldo);
        Assert.Empty(db.MovimentacoesEstoqueEpi);
    }

    [Fact]
    public async Task Handle_EntregaInexistente_LancaKeyNotFound()
    {
        var db = DbContextFactory.Criar();
        var handler = new ExcluirEntregaEpiCommandHandler(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new ExcluirEntregaEpiCommand(Guid.NewGuid()), default));
    }
}

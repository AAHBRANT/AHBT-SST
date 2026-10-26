using AAHBRANT.SST.Application.EntregasEpi.Commands;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.EntregasEpi;

// Teste E2E 21/10: editar quantidade/EPI de uma entrega não acertava o estoque, e a exclusão depois
// estornava o valor já editado — o saldo terminava com unidades a mais (ou trocadas entre itens).
public class AtualizarEntregaEpiEstoqueTests
{
    private static async Task<(Trabalhador trabalhador, CatalogoEpi capacete, CatalogoEpi bota)> SemearAsync(SstDbContext db)
    {
        var trabalhador = new Trabalhador { ObraId = Guid.NewGuid(), Funcao = new Funcao { Nome = "Pedreiro" }, Nome = "Carlos Eduardo", Cpf = "00000000000" };
        var capacete = new CatalogoEpi { Nome = "Capacete de Segurança", VidaUtilEmMeses = 12 };
        var bota = new CatalogoEpi { Nome = "Bota de Segurança", VidaUtilEmMeses = 12 };
        db.Trabalhadores.Add(trabalhador);
        db.CatalogoEpis.AddRange(capacete, bota);
        db.EstoquesEpi.AddRange(
            new EstoqueEpi { CatalogoEpiId = capacete.Id, ObraId = trabalhador.ObraId, Saldo = 10 },
            new EstoqueEpi { CatalogoEpiId = bota.Id, ObraId = trabalhador.ObraId, Saldo = 5 });
        await db.SaveChangesAsync();
        await Nr6DeTeste.SemearAsync(db, trabalhador.Id);
        return (trabalhador, capacete, bota);
    }

    private static CriarEntregaEpiCommand Criar(Trabalhador t, CatalogoEpi c, int quantidade) =>
        new(t.Id, c.Id, new DateTime(2026, 10, 21), null, null, quantidade, null, null, null, MotivoEntregaEpi.Inicial, null, null);

    private static AtualizarEntregaEpiCommand Atualizar(Guid id, Trabalhador t, CatalogoEpi c, int quantidade,
        DateTime? dataDevolucao = null, int? quantidadeDevolucao = null) =>
        new(id, t.Id, c.Id, new DateTime(2026, 10, 21), dataDevolucao, null, quantidade, quantidadeDevolucao,
            null, null, null, MotivoEntregaEpi.Inicial, null, null);

    private static async Task<int> SaldoAsync(SstDbContext db, CatalogoEpi c) =>
        (await db.EstoquesEpi.SingleAsync(e => e.CatalogoEpiId == c.Id)).Saldo;

    [Fact]
    public async Task Handle_AumentaQuantidade_BaixaADiferencaDoEstoque()
    {
        var db = DbContextFactory.Criar();
        var (t, capacete, _) = await SemearAsync(db);
        var id = await new CriarEntregaEpiCommandHandler(db).Handle(Criar(t, capacete, 1), default);

        await new AtualizarEntregaEpiCommandHandler(db).Handle(Atualizar(id, t, capacete, 3), default);

        Assert.Equal(7, await SaldoAsync(db, capacete));
    }

    [Fact]
    public async Task Handle_ReduzQuantidade_DevolveADiferencaAoEstoque()
    {
        var db = DbContextFactory.Criar();
        var (t, capacete, _) = await SemearAsync(db);
        var id = await new CriarEntregaEpiCommandHandler(db).Handle(Criar(t, capacete, 4), default);

        await new AtualizarEntregaEpiCommandHandler(db).Handle(Atualizar(id, t, capacete, 1), default);

        Assert.Equal(9, await SaldoAsync(db, capacete));
    }

    // O cenário exato do teste E2E: editar 1→2 e excluir tem de voltar ao saldo original.
    [Fact]
    public async Task Handle_EditarEExcluir_SaldoVoltaAoOriginal()
    {
        var db = DbContextFactory.Criar();
        var (t, capacete, _) = await SemearAsync(db);
        var id = await new CriarEntregaEpiCommandHandler(db).Handle(Criar(t, capacete, 1), default);

        await new AtualizarEntregaEpiCommandHandler(db).Handle(Atualizar(id, t, capacete, 2), default);
        await new ExcluirEntregaEpiCommandHandler(db).Handle(new ExcluirEntregaEpiCommand(id), default);

        Assert.Equal(10, await SaldoAsync(db, capacete));
    }

    [Fact]
    public async Task Handle_TrocaOEpi_EstornaOAntigoEBaixaONovo()
    {
        var db = DbContextFactory.Criar();
        var (t, capacete, bota) = await SemearAsync(db);
        var id = await new CriarEntregaEpiCommandHandler(db).Handle(Criar(t, capacete, 2), default);

        await new AtualizarEntregaEpiCommandHandler(db).Handle(Atualizar(id, t, bota, 2), default);
        Assert.Equal(10, await SaldoAsync(db, capacete));
        Assert.Equal(3, await SaldoAsync(db, bota));

        await new ExcluirEntregaEpiCommandHandler(db).Handle(new ExcluirEntregaEpiCommand(id), default);
        Assert.Equal(10, await SaldoAsync(db, capacete));
        Assert.Equal(5, await SaldoAsync(db, bota));
    }

    [Fact]
    public async Task Handle_AumentoAlemDoSaldo_ERecusadoSemMexerNoEstoque()
    {
        var db = DbContextFactory.Criar();
        var (t, _, bota) = await SemearAsync(db);
        var id = await new CriarEntregaEpiCommandHandler(db).Handle(Criar(t, bota, 1), default);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AtualizarEntregaEpiCommandHandler(db).Handle(Atualizar(id, t, bota, 10), default));

        Assert.Contains("Estoque insuficiente", ex.Message);
        Assert.Equal(4, await SaldoAsync(db, bota));
    }

    [Fact]
    public async Task Handle_SoRegistraDevolucao_RepoeEstoqueComoAntes()
    {
        var db = DbContextFactory.Criar();
        var (t, capacete, _) = await SemearAsync(db);
        var id = await new CriarEntregaEpiCommandHandler(db).Handle(Criar(t, capacete, 2), default);

        await new AtualizarEntregaEpiCommandHandler(db).Handle(
            Atualizar(id, t, capacete, 2, dataDevolucao: new DateTime(2026, 10, 25), quantidadeDevolucao: 2), default);

        Assert.Equal(10, await SaldoAsync(db, capacete));
    }

    [Fact]
    public async Task Handle_AlterarQuantidadeDepoisDaDevolucao_ERecusado()
    {
        var db = DbContextFactory.Criar();
        var (t, capacete, _) = await SemearAsync(db);
        var id = await new CriarEntregaEpiCommandHandler(db).Handle(Criar(t, capacete, 2), default);
        var devolucao = new DateTime(2026, 10, 25);
        await new AtualizarEntregaEpiCommandHandler(db).Handle(Atualizar(id, t, capacete, 2, devolucao, 2), default);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AtualizarEntregaEpiCommandHandler(db).Handle(Atualizar(id, t, capacete, 3, devolucao, 2), default));

        Assert.Equal(10, await SaldoAsync(db, capacete));
    }
}

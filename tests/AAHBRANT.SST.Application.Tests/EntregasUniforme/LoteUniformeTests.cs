using AAHBRANT.SST.Application.CatalogosUniforme;
using AAHBRANT.SST.Application.CatalogosUniforme.Commands;
using AAHBRANT.SST.Application.CatalogosUniforme.Queries;
using AAHBRANT.SST.Application.EntregasUniforme.Commands;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.EntregasUniforme;

public class LoteUniformeTests
{
    [Fact]
    public async Task AtualizarNomeEModelo_PreservaIdEstoqueHistoricoEPreferencia()
    {
        using var db = DbContextFactory.Criar();
        var peca = new CatalogoUniforme { Nome = "camisa brom manga longa", FotoConteudo = [1, 2, 3] };
        var obra = new Obra { Nome = "Obra" };
        db.CatalogoUniformes.Add(peca); db.Obras.Add(obra);
        var estoque = new EstoqueUniforme { CatalogoUniformeId = peca.Id, ObraId = obra.Id, Tamanho = "M", Saldo = 17 };
        var entrega = new EntregaUniforme { CatalogoUniformeId = peca.Id, Tamanho = "G", Quantidade = 2 };
        var preferencia = new TrabalhadorTamanhoUniforme { CatalogoUniformeId = peca.Id, Tamanho = "P" };
        db.EstoquesUniforme.Add(estoque); db.EntregasUniforme.Add(entrega); db.TrabalhadorTamanhosUniforme.Add(preferencia);
        await db.SaveChangesAsync();
        await new AtualizarCatalogoUniformeCommandHandler(db).Handle(new(peca.Id, " camisa brom manga longa ", null, "camisa-brim-laranja"), default);
        Assert.Equal("CAMISA BRIM MANGA LONGA", peca.Nome);
        Assert.Equal(17, (await db.EstoquesUniforme.SingleAsync()).Saldo);
        Assert.Equal(peca.Id, (await db.EntregasUniforme.SingleAsync()).CatalogoUniformeId);
        Assert.Equal("G", entrega.Tamanho); Assert.Equal("P", preferencia.Tamanho);
        Assert.Equal(new byte[] { 1, 2, 3 }, peca.FotoConteudo);
        var foto = await new ObterFotoCatalogoUniformeQueryHandler(db).Handle(new(peca.Id), default);
        Assert.NotNull(foto); Assert.Equal("image/jpeg", foto.ContentType);
        Assert.True(foto.Conteudo.Length > 1000); Assert.Equal(0xff, foto.Conteudo[0]);
        Assert.Single(await db.CatalogoUniformes.ToListAsync());
    }

    [Fact]
    public async Task TodasAsReferencias_PossuemFichaIncorporada()
    {
        foreach (var chave in ReferenciasUniforme.Chaves)
            Assert.True((await ReferenciasUniforme.LerFotoAsync(chave, default)).Length > 1000);
        Assert.False(ReferenciasUniforme.Valida("../arquivo"));
    }

    private static async Task<(Trabalhador, CatalogoUniforme, CatalogoUniforme)> Preparar(Infrastructure.Persistencia.SstDbContext db, int saldoCalca)
    {
        var obra = new Obra { Nome = "Obra" }; var funcao = new Funcao { Nome = "Carpinteiro" };
        var trabalhador = new Trabalhador { Nome = "Pessoa de teste", Cpf = "12345678900", Matricula = "1", Obra = obra, Funcao = funcao, DataAdmissao = DateTime.UtcNow };
        var camisa = new CatalogoUniforme { Nome = "CAMISA BRIM MANGA LONGA" }; var calca = new CatalogoUniforme { Nome = "CALÇA BRIM" };
        db.Trabalhadores.Add(trabalhador); db.CatalogoUniformes.AddRange(camisa, calca);
        db.MatrizUniformeFuncoes.AddRange(new MatrizUniformeFuncao { FuncaoId = funcao.Id, CatalogoUniformeId = camisa.Id }, new MatrizUniformeFuncao { FuncaoId = funcao.Id, CatalogoUniformeId = calca.Id });
        db.EstoquesUniforme.AddRange(new EstoqueUniforme { CatalogoUniformeId = camisa.Id, ObraId = obra.Id, Tamanho = "GG", Saldo = 10 }, new EstoqueUniforme { CatalogoUniformeId = calca.Id, ObraId = obra.Id, Tamanho = "46", Saldo = saldoCalca });
        db.TrabalhadorTamanhosUniforme.Add(new TrabalhadorTamanhoUniforme { TrabalhadorId = trabalhador.Id, CatalogoUniformeId = camisa.Id, Tamanho = "M" });
        await db.SaveChangesAsync(); return (trabalhador, camisa, calca);
    }

    [Fact]
    public async Task Lote_UsaTamanhoSelecionadoEBaixaApenasGradesDoKit()
    {
        using var db = DbContextFactory.Criar(); var (t, camisa, calca) = await Preparar(db, 5);
        var ids = await new CriarLoteEntregaUniformeCommandHandler(db).Handle(new(t.Id, DateTime.UtcNow, MotivoEntregaUniforme.Inicial, null, [new(camisa.Id, "gg", 2), new(calca.Id, "46", 1)]), default);
        Assert.Equal(2, ids.Count); Assert.Equal(2, await db.EntregasUniforme.CountAsync());
        Assert.Equal(8, (await db.EstoquesUniforme.SingleAsync(s => s.CatalogoUniformeId == camisa.Id)).Saldo);
        Assert.Equal(4, (await db.EstoquesUniforme.SingleAsync(s => s.CatalogoUniformeId == calca.Id)).Saldo);
        Assert.Equal("GG", (await db.EntregasUniforme.SingleAsync(e => e.CatalogoUniformeId == camisa.Id)).Tamanho);
        Assert.Equal("M", (await db.TrabalhadorTamanhosUniforme.SingleAsync()).Tamanho);
        Assert.Equal(2, await db.MovimentacoesEstoqueUniforme.CountAsync());
    }

    [Fact]
    public async Task Lote_SegundaPecaSemSaldo_NaoPersistePrimeiraEntregaNemBaixa()
    {
        using var db = DbContextFactory.Criar(); var (t, camisa, calca) = await Preparar(db, 0);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new CriarLoteEntregaUniformeCommandHandler(db).Handle(new(t.Id, DateTime.UtcNow, MotivoEntregaUniforme.Inicial, null, [new(camisa.Id, "GG", 2), new(calca.Id, "46", 1)]), default));
        db.ChangeTracker.Clear();
        Assert.Empty(await db.EntregasUniforme.ToListAsync()); Assert.Empty(await db.MovimentacoesEstoqueUniforme.ToListAsync());
        Assert.Equal(10, (await db.EstoquesUniforme.SingleAsync(s => s.CatalogoUniformeId == camisa.Id)).Saldo);
    }

    [Fact]
    public async Task Lote_ReenvioDaOperacao_NaoDuplicaEntregaOuBaixa()
    {
        using var db = DbContextFactory.Criar(); var (t, camisa, calca) = await Preparar(db, 5);
        var command = new CriarLoteEntregaUniformeCommand(t.Id, DateTime.UtcNow, MotivoEntregaUniforme.Inicial, null,
            [new(camisa.Id, "GG", 2), new(calca.Id, "46", 1)], Guid.NewGuid());
        var handler = new CriarLoteEntregaUniformeCommandHandler(db);
        var primeira = await handler.Handle(command, default);
        db.ChangeTracker.Clear();
        var segunda = await handler.Handle(command, default);
        Assert.Equal(primeira, segunda);
        Assert.Equal(2, await db.EntregasUniforme.CountAsync());
        Assert.Equal(2, await db.MovimentacoesEstoqueUniforme.CountAsync());
        Assert.Equal(8, (await db.EstoquesUniforme.SingleAsync(s => s.CatalogoUniformeId == camisa.Id)).Saldo);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command with { Observacoes = "Kit alterado" }, default));
    }

    [Fact]
    public async Task ConsultaCatalogo_MostraFuncoesDaMatrizSemInventarAtribuicoes()
    {
        using var db = DbContextFactory.Criar(); var (_, camisa, _) = await Preparar(db, 5);
        var lista = await new ListarCatalogosUniformeQueryHandler(db).Handle(new(), default);
        Assert.Equal(new[] { "Carpinteiro" }, lista.Single(p => p.Id == camisa.Id).FuncoesAtribuidas);
    }

    [Fact]
    public async Task Lote_PecaDuplicada_RecusaAntesDeGravar()
    {
        using var db = DbContextFactory.Criar(); var (t, camisa, _) = await Preparar(db, 5);
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => new CriarLoteEntregaUniformeCommandHandler(db).Handle(new(t.Id, DateTime.UtcNow, MotivoEntregaUniforme.Inicial, null, [new(camisa.Id, "GG", 1), new(camisa.Id, "GG", 1)]), default));
        Assert.Empty(await db.EntregasUniforme.ToListAsync());
    }
}

using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Funcoes.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Funcoes;

public class ExcluirFuncoesOrfasCommandHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_FuncaoOrfaValida_ExcluiComoSoftDelete()
    {
        var db = CriarDb(nameof(Handle_FuncaoOrfaValida_ExcluiComoSoftDelete));
        var funcao = new Funcao { Nome = "Função Lixo", CboCodigo = null };
        db.Funcoes.Add(funcao);
        await db.SaveChangesAsync();

        var handler = new ExcluirFuncoesOrfasCommandHandler(db);
        var quantidade = await handler.Handle(new ExcluirFuncoesOrfasCommand(new List<Guid> { funcao.Id }), default);

        Assert.Equal(1, quantidade);
        Assert.False(await db.Funcoes.AnyAsync(f => f.Id == funcao.Id));
        var funcaoAtualizada = await db.Funcoes.IgnoreQueryFilters().SingleAsync(f => f.Id == funcao.Id);
        Assert.False(funcaoAtualizada.Ativo);
    }

    [Fact]
    public async Task Handle_FuncaoComCboNaListaPorEngano_IgnoraENaoExclui()
    {
        var db = CriarDb(nameof(Handle_FuncaoComCboNaListaPorEngano_IgnoraENaoExclui));
        var funcao = new Funcao { Nome = "Analista Financeiro", CboCodigo = "4110-05" };
        db.Funcoes.Add(funcao);
        await db.SaveChangesAsync();

        var handler = new ExcluirFuncoesOrfasCommandHandler(db);
        var quantidade = await handler.Handle(new ExcluirFuncoesOrfasCommand(new List<Guid> { funcao.Id }), default);

        Assert.Equal(0, quantidade);
        Assert.True(await db.Funcoes.AnyAsync(f => f.Id == funcao.Id));
    }

    [Fact]
    public async Task Handle_TrabalhadorFoiVinculadoDepoisDaListaCarregar_IgnoraENaoExclui()
    {
        // Defesa contra corrida: a tela pode ter carregado a lista de órfãs antes de alguém vincular
        // um trabalhador nessa função — o comando reconfirma na hora de excluir, não confia cegamente
        // na lista que veio da tela.
        var db = CriarDb(nameof(Handle_TrabalhadorFoiVinculadoDepoisDaListaCarregar_IgnoraENaoExclui));
        var funcao = new Funcao { Nome = "Motorista", CboCodigo = null };
        var obra = new Obra { Nome = "Obra Teste", Codigo = "OBRA-1" };
        db.Funcoes.Add(funcao);
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        db.Trabalhadores.Add(new Trabalhador { Nome = "Fulano", ObraId = obra.Id, FuncaoId = funcao.Id });
        await db.SaveChangesAsync();

        var handler = new ExcluirFuncoesOrfasCommandHandler(db);
        var quantidade = await handler.Handle(new ExcluirFuncoesOrfasCommand(new List<Guid> { funcao.Id }), default);

        Assert.Equal(0, quantidade);
        Assert.True(await db.Funcoes.AnyAsync(f => f.Id == funcao.Id));
    }

    [Fact]
    public async Task Handle_VariasFuncoesOrfasDeUmaVez_ExcluiTodasEContaCorretamente()
    {
        var db = CriarDb(nameof(Handle_VariasFuncoesOrfasDeUmaVez_ExcluiTodasEContaCorretamente));
        var funcao1 = new Funcao { Nome = "Função Lixo 1", CboCodigo = null };
        var funcao2 = new Funcao { Nome = "Função Lixo 2", CboCodigo = null };
        db.Funcoes.AddRange(funcao1, funcao2);
        await db.SaveChangesAsync();

        var handler = new ExcluirFuncoesOrfasCommandHandler(db);
        var quantidade = await handler.Handle(
            new ExcluirFuncoesOrfasCommand(new List<Guid> { funcao1.Id, funcao2.Id }), default);

        Assert.Equal(2, quantidade);
        Assert.False(await db.Funcoes.AnyAsync(f => f.Id == funcao1.Id || f.Id == funcao2.Id));
    }
}

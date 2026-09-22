using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Funcoes.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Funcoes;

public class ListarFuncoesInativasComTrabalhadorQueryHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_FuncaoInativaComTrabalhadorAtivo_Aparece()
    {
        var db = CriarDb(nameof(Handle_FuncaoInativaComTrabalhadorAtivo_Aparece));
        var funcao = new Funcao { Nome = "Técnico de Segurança do Trabalho", CboCodigo = "3516-05" };
        var obra = new Obra { Nome = "Obra Teste", Codigo = "OBRA-1" };
        db.Funcoes.Add(funcao);
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        db.Trabalhadores.Add(new Trabalhador { Nome = "Gabriel", ObraId = obra.Id, FuncaoId = funcao.Id });
        await db.SaveChangesAsync();

        // Simula o botão "Excluir" de linha (soft-delete, sem checar trabalhador vinculado)
        funcao.Ativo = false;
        await db.SaveChangesAsync();

        var handler = new ListarFuncoesInativasComTrabalhadorQueryHandler(db);
        var resultado = await handler.Handle(new ListarFuncoesInativasComTrabalhadorQuery(), default);

        var item = Assert.Single(resultado);
        Assert.Equal(funcao.Id, item.Id);
        Assert.Equal("Técnico de Segurança do Trabalho", item.Nome);
        Assert.Equal(1, item.QuantidadeTrabalhadores);
    }

    [Fact]
    public async Task Handle_FuncaoInativaComVariosTrabalhadoresAtivos_ContaTodos()
    {
        var db = CriarDb(nameof(Handle_FuncaoInativaComVariosTrabalhadoresAtivos_ContaTodos));
        var funcao = new Funcao { Nome = "Pedreiro", CboCodigo = "7152-10" };
        var obra = new Obra { Nome = "Obra Teste", Codigo = "OBRA-1" };
        db.Funcoes.Add(funcao);
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        db.Trabalhadores.AddRange(
            new Trabalhador { Nome = "A", ObraId = obra.Id, FuncaoId = funcao.Id },
            new Trabalhador { Nome = "B", ObraId = obra.Id, FuncaoId = funcao.Id },
            new Trabalhador { Nome = "C", ObraId = obra.Id, FuncaoId = funcao.Id });
        await db.SaveChangesAsync();

        funcao.Ativo = false;
        await db.SaveChangesAsync();

        var handler = new ListarFuncoesInativasComTrabalhadorQueryHandler(db);
        var resultado = await handler.Handle(new ListarFuncoesInativasComTrabalhadorQuery(), default);

        var item = Assert.Single(resultado);
        Assert.Equal(3, item.QuantidadeTrabalhadores);
    }

    [Fact]
    public async Task Handle_FuncaoInativaSemTrabalhador_NaoAparece()
    {
        var db = CriarDb(nameof(Handle_FuncaoInativaSemTrabalhador_NaoAparece));
        var funcao = new Funcao { Nome = "Função Sem Uso", CboCodigo = null, Ativo = false };
        db.Funcoes.Add(funcao);
        await db.SaveChangesAsync();

        var handler = new ListarFuncoesInativasComTrabalhadorQueryHandler(db);
        var resultado = await handler.Handle(new ListarFuncoesInativasComTrabalhadorQuery(), default);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task Handle_FuncaoAtivaComTrabalhador_NaoAparece()
    {
        var db = CriarDb(nameof(Handle_FuncaoAtivaComTrabalhador_NaoAparece));
        var funcao = new Funcao { Nome = "Encarregado", CboCodigo = "7102-05" };
        var obra = new Obra { Nome = "Obra Teste", Codigo = "OBRA-1" };
        db.Funcoes.Add(funcao);
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        db.Trabalhadores.Add(new Trabalhador { Nome = "Fulano", ObraId = obra.Id, FuncaoId = funcao.Id });
        await db.SaveChangesAsync();

        var handler = new ListarFuncoesInativasComTrabalhadorQueryHandler(db);
        var resultado = await handler.Handle(new ListarFuncoesInativasComTrabalhadorQuery(), default);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task Handle_FuncaoInativaComTrabalhadorInativo_NaoAparece()
    {
        var db = CriarDb(nameof(Handle_FuncaoInativaComTrabalhadorInativo_NaoAparece));
        var funcao = new Funcao { Nome = "Função Antiga", CboCodigo = null };
        var obra = new Obra { Nome = "Obra Teste", Codigo = "OBRA-1" };
        db.Funcoes.Add(funcao);
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        var trabalhador = new Trabalhador { Nome = "Desligado", ObraId = obra.Id, FuncaoId = funcao.Id };
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();

        funcao.Ativo = false;
        trabalhador.Ativo = false;
        await db.SaveChangesAsync();

        var handler = new ListarFuncoesInativasComTrabalhadorQueryHandler(db);
        var resultado = await handler.Handle(new ListarFuncoesInativasComTrabalhadorQuery(), default);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task Handle_TrabalhadorDeOutraObraForaDoEscopoRbac_AindaAssimAparece()
    {
        // Mesmo raciocínio do teste equivalente em ListarFuncoesOrfasQueryHandlerTests: um admin
        // com escopo restrito a outra obra não pode "esconder" o diagnóstico de emergência.
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nameof(Handle_TrabalhadorDeOutraObraForaDoEscopoRbac_AindaAssimAparece))
            .Options;
        var usuarioAtual = new CurrentUserService();
        IAppDbContext db = new SstDbContext(options, usuarioAtual);

        var funcao = new Funcao { Nome = "Motorista", CboCodigo = null };
        var obraDoTrabalhador = new Obra { Nome = "Obra Fora do Escopo", Codigo = "OBRA-2" };
        var obraPermitidaAoAdmin = new Obra { Nome = "Outra Obra", Codigo = "OBRA-3" };
        db.Funcoes.Add(funcao);
        db.Obras.AddRange(obraDoTrabalhador, obraPermitidaAoAdmin);
        await db.SaveChangesAsync();

        db.Trabalhadores.Add(new Trabalhador { Nome = "Ciclano", ObraId = obraDoTrabalhador.Id, FuncaoId = funcao.Id });
        await db.SaveChangesAsync();

        funcao.Ativo = false;
        await db.SaveChangesAsync();

        usuarioAtual.DefinirEscopo(temAcessoGlobal: false, obrasPermitidas: new[] { obraPermitidaAoAdmin.Id });

        var handler = new ListarFuncoesInativasComTrabalhadorQueryHandler(db);
        var resultado = await handler.Handle(new ListarFuncoesInativasComTrabalhadorQuery(), default);

        Assert.Single(resultado);
    }
}

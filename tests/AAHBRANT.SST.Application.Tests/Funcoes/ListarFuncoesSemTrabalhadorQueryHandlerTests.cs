using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Funcoes.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Funcoes;

public class ListarFuncoesSemTrabalhadorQueryHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_FuncaoSemTrabalhador_Aparece()
    {
        var db = CriarDb(nameof(Handle_FuncaoSemTrabalhador_Aparece));
        db.Funcoes.Add(new Funcao { Nome = "Função Vazia", CboCodigo = "1234-56" });
        await db.SaveChangesAsync();

        var handler = new ListarFuncoesSemTrabalhadorQueryHandler(db);
        var resultado = await handler.Handle(new ListarFuncoesSemTrabalhadorQuery(), default);

        var item = Assert.Single(resultado);
        Assert.Equal("Função Vazia", item.Nome);
        Assert.False(item.TemEpiNaMatriz);
        Assert.False(item.TemTreinamentoNaMatriz);
        Assert.False(item.TemUniformeNaMatriz);
    }

    [Fact]
    public async Task Handle_FuncaoComTrabalhadorAtivo_NaoAparece()
    {
        var db = CriarDb(nameof(Handle_FuncaoComTrabalhadorAtivo_NaoAparece));
        var funcao = new Funcao { Nome = "Pedreiro", CboCodigo = "7152-10" };
        var obra = new Obra { Nome = "Obra Teste", Codigo = "OBRA-1" };
        db.Funcoes.Add(funcao);
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        db.Trabalhadores.Add(new Trabalhador { Nome = "Fulano", ObraId = obra.Id, FuncaoId = funcao.Id });
        await db.SaveChangesAsync();

        var handler = new ListarFuncoesSemTrabalhadorQueryHandler(db);
        var resultado = await handler.Handle(new ListarFuncoesSemTrabalhadorQuery(), default);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task Handle_FuncaoComApenasTrabalhadorInativo_Aparece()
    {
        var db = CriarDb(nameof(Handle_FuncaoComApenasTrabalhadorInativo_Aparece));
        var funcao = new Funcao { Nome = "Função Antiga", CboCodigo = null };
        var obra = new Obra { Nome = "Obra Teste", Codigo = "OBRA-1" };
        db.Funcoes.Add(funcao);
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        var trabalhador = new Trabalhador { Nome = "Desligado", ObraId = obra.Id, FuncaoId = funcao.Id };
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();

        trabalhador.Ativo = false;
        await db.SaveChangesAsync();

        var handler = new ListarFuncoesSemTrabalhadorQueryHandler(db);
        var resultado = await handler.Handle(new ListarFuncoesSemTrabalhadorQuery(), default);

        Assert.Single(resultado);
    }

    [Fact]
    public async Task Handle_FuncaoSemTrabalhadorMasComEpiNaMatriz_AindaAssimAparece_ComFlagMarcada()
    {
        var db = CriarDb(nameof(Handle_FuncaoSemTrabalhadorMasComEpiNaMatriz_AindaAssimAparece_ComFlagMarcada));
        var funcao = new Funcao { Nome = "Soldador", CboCodigo = "7243-20" };
        var epi = new CatalogoEpi { Nome = "Capacete", VidaUtilEmMeses = 12 };
        db.Funcoes.Add(funcao);
        db.CatalogoEpis.Add(epi);
        await db.SaveChangesAsync();

        db.MatrizEpiFuncoes.Add(new MatrizEpiFuncao { FuncaoId = funcao.Id, CatalogoEpiId = epi.Id });
        await db.SaveChangesAsync();

        var handler = new ListarFuncoesSemTrabalhadorQueryHandler(db);
        var resultado = await handler.Handle(new ListarFuncoesSemTrabalhadorQuery(), default);

        var item = Assert.Single(resultado);
        Assert.True(item.TemEpiNaMatriz);
    }

    [Fact]
    public async Task Handle_TrabalhadorDeOutraObraForaDoEscopoRbac_AindaAssimProtegeAFuncao()
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nameof(Handle_TrabalhadorDeOutraObraForaDoEscopoRbac_AindaAssimProtegeAFuncao))
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

        usuarioAtual.DefinirEscopo(temAcessoGlobal: false, obrasPermitidas: new[] { obraPermitidaAoAdmin.Id });

        var handler = new ListarFuncoesSemTrabalhadorQueryHandler(db);
        var resultado = await handler.Handle(new ListarFuncoesSemTrabalhadorQuery(), default);

        Assert.Empty(resultado);
    }
}

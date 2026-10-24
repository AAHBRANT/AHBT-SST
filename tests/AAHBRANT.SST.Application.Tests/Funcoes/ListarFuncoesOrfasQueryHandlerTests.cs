using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Funcoes.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Funcoes;

public class ListarFuncoesOrfasQueryHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_FuncaoSemCboSemEpiSemTrabalhador_ApareceComoOrfa()
    {
        var db = CriarDb(nameof(Handle_FuncaoSemCboSemEpiSemTrabalhador_ApareceComoOrfa));
        db.Funcoes.Add(new Funcao { Nome = "Função Lixo", CboCodigo = null });
        await db.SaveChangesAsync();

        var handler = new ListarFuncoesOrfasQueryHandler(db);
        var resultado = await handler.Handle(new ListarFuncoesOrfasQuery(), default);

        var item = Assert.Single(resultado);
        Assert.Equal("Função Lixo", item.Nome);
    }

    [Fact]
    public async Task Handle_FuncaoComCbo_NuncaAparece()
    {
        var db = CriarDb(nameof(Handle_FuncaoComCbo_NuncaAparece));
        db.Funcoes.Add(new Funcao { Nome = "Analista Financeiro", CboCodigo = "4110-05" });
        await db.SaveChangesAsync();

        var handler = new ListarFuncoesOrfasQueryHandler(db);
        var resultado = await handler.Handle(new ListarFuncoesOrfasQuery(), default);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task Handle_FuncaoSemCboComEpiNaMatriz_NaoAparece()
    {
        var db = CriarDb(nameof(Handle_FuncaoSemCboComEpiNaMatriz_NaoAparece));
        var funcao = new Funcao { Nome = "Soldador", CboCodigo = null };
        var epi = new CatalogoEpi { Nome = "Capacete", VidaUtilEmMeses = 12 };
        db.Funcoes.Add(funcao);
        db.CatalogoEpis.Add(epi);
        await db.SaveChangesAsync();

        db.MatrizEpiFuncoes.Add(new MatrizEpiFuncao { FuncaoId = funcao.Id, CatalogoEpiId = epi.Id });
        await db.SaveChangesAsync();

        var handler = new ListarFuncoesOrfasQueryHandler(db);
        var resultado = await handler.Handle(new ListarFuncoesOrfasQuery(), default);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task Handle_FuncaoSemCboComTrabalhadorVinculado_NaoAparece()
    {
        var db = CriarDb(nameof(Handle_FuncaoSemCboComTrabalhadorVinculado_NaoAparece));
        var funcao = new Funcao { Nome = "Analista Financeiro", CboCodigo = null };
        var obra = new Obra { Nome = "Obra Teste", Codigo = "OBRA-1" };
        db.Funcoes.Add(funcao);
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        db.Trabalhadores.Add(new Trabalhador { Nome = "Fulano", ObraId = obra.Id, FuncaoId = funcao.Id });
        await db.SaveChangesAsync();

        var handler = new ListarFuncoesOrfasQueryHandler(db);
        var resultado = await handler.Handle(new ListarFuncoesOrfasQuery(), default);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task Handle_FuncaoSemCboComTreinamentoNaMatriz_NaoAparece()
    {
        var db = CriarDb(nameof(Handle_FuncaoSemCboComTreinamentoNaMatriz_NaoAparece));
        var funcao = new Funcao { Nome = "Encarregado", CboCodigo = null };
        var curso = new CursoTreinamento { Nome = "NR-35" };
        db.Funcoes.Add(funcao);
        db.CursosTreinamento.Add(curso);
        await db.SaveChangesAsync();

        db.MatrizTreinamentoFuncoes.Add(new MatrizTreinamentoFuncao { FuncaoId = funcao.Id, CursoTreinamentoId = curso.Id });
        await db.SaveChangesAsync();

        var handler = new ListarFuncoesOrfasQueryHandler(db);
        var resultado = await handler.Handle(new ListarFuncoesOrfasQuery(), default);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task Handle_FuncaoSemCboComUniformeNaMatriz_NaoAparece()
    {
        var db = CriarDb(nameof(Handle_FuncaoSemCboComUniformeNaMatriz_NaoAparece));
        var funcao = new Funcao { Nome = "Ajudante Geral", CboCodigo = null };
        var uniforme = new CatalogoUniforme { Nome = "Camisa" };
        db.Funcoes.Add(funcao);
        db.CatalogoUniformes.Add(uniforme);
        await db.SaveChangesAsync();

        db.MatrizUniformeFuncoes.Add(new MatrizUniformeFuncao { FuncaoId = funcao.Id, CatalogoUniformeId = uniforme.Id });
        await db.SaveChangesAsync();

        var handler = new ListarFuncoesOrfasQueryHandler(db);
        var resultado = await handler.Handle(new ListarFuncoesOrfasQuery(), default);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task Handle_TrabalhadorDeOutraObraForaDoEscopoRbac_AindaAssimProtegeAFuncao()
    {
        // Simula um admin com acesso restrito a uma obra diferente da do trabalhador (sem isso o
        // teste passaria de qualquer jeito, já que CurrentUserService() sem DefinirEscopo() default
        // pra acesso global) — mesmo com o escopo restrito, a função com trabalhador vinculado numa
        // obra "fora do escopo" dele não pode aparecer como órfã (ver IgnoreQueryFilters em
        // DeteccaoFuncaoOrfa — sem isso, o filtro de RBAC do Trabalhador esconderia esse vínculo).
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

        var handler = new ListarFuncoesOrfasQueryHandler(db);
        var resultado = await handler.Handle(new ListarFuncoesOrfasQuery(), default);

        Assert.Empty(resultado);
    }
}

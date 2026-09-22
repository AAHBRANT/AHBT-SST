using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Funcoes.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Funcoes;

public class ListarFuncoesDuplicadasQueryHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_UmaComCboEOutraSem_DetectaComoDuplicataResolvivel()
    {
        var db = CriarDb(nameof(Handle_UmaComCboEOutraSem_DetectaComoDuplicataResolvivel));
        var comCbo = new Funcao { Nome = "Técnico de Segurança do Trabalho", CboCodigo = "3516-05" };
        var semCbo = new Funcao { Nome = "Técnico de Segurança do Trabalho", CboCodigo = null };
        db.Funcoes.AddRange(comCbo, semCbo);
        await db.SaveChangesAsync();

        var handler = new ListarFuncoesDuplicadasQueryHandler(db);
        var resultado = await handler.Handle(new ListarFuncoesDuplicadasQuery(), default);

        var grupo = Assert.Single(resultado);
        Assert.Equal("Técnico de Segurança do Trabalho", grupo.Nome);
        Assert.Equal(comCbo.Id, grupo.Manter.Id);
        var remover = Assert.Single(grupo.Remover);
        Assert.Equal(semCbo.Id, remover.Id);
    }

    [Fact]
    public async Task Handle_DuasComCbo_NaoResolveSozinho()
    {
        var db = CriarDb(nameof(Handle_DuasComCbo_NaoResolveSozinho));
        db.Funcoes.AddRange(
            new Funcao { Nome = "Pedreiro", CboCodigo = "7152-10" },
            new Funcao { Nome = "Pedreiro", CboCodigo = "7152-15" });
        await db.SaveChangesAsync();

        var handler = new ListarFuncoesDuplicadasQueryHandler(db);
        var resultado = await handler.Handle(new ListarFuncoesDuplicadasQuery(), default);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task Handle_NenhumaComCbo_NaoResolveSozinho()
    {
        var db = CriarDb(nameof(Handle_NenhumaComCbo_NaoResolveSozinho));
        db.Funcoes.AddRange(
            new Funcao { Nome = "Ajudante Geral", CboCodigo = null },
            new Funcao { Nome = "Ajudante Geral", CboCodigo = null });
        await db.SaveChangesAsync();

        var handler = new ListarFuncoesDuplicadasQueryHandler(db);
        var resultado = await handler.Handle(new ListarFuncoesDuplicadasQuery(), default);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task Handle_FuncaoSemCboSemDuplicata_NaoAparece()
    {
        // Função sem CBO mas com nome único (nunca sincronizada com o G-RH) é uma função real — não
        // deve ser sinalizada pra remoção só por não ter CBO.
        var db = CriarDb(nameof(Handle_FuncaoSemCboSemDuplicata_NaoAparece));
        db.Funcoes.Add(new Funcao { Nome = "Apontador de Obra", CboCodigo = null });
        await db.SaveChangesAsync();

        var handler = new ListarFuncoesDuplicadasQueryHandler(db);
        var resultado = await handler.Handle(new ListarFuncoesDuplicadasQuery(), default);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task Handle_ContaTrabalhadoresDeCadaFuncaoNoGrupo()
    {
        var db = CriarDb(nameof(Handle_ContaTrabalhadoresDeCadaFuncaoNoGrupo));
        var comCbo = new Funcao { Nome = "Motorista", CboCodigo = "7825-10" };
        var semCbo = new Funcao { Nome = "Motorista", CboCodigo = null };
        var obra = new Obra { Nome = "Obra Teste", Codigo = "OBRA-1" };
        db.Funcoes.AddRange(comCbo, semCbo);
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        db.Trabalhadores.Add(new Trabalhador { Nome = "Fulano", ObraId = obra.Id, FuncaoId = semCbo.Id });
        db.Trabalhadores.Add(new Trabalhador { Nome = "Beltrano", ObraId = obra.Id, FuncaoId = comCbo.Id });
        await db.SaveChangesAsync();

        var handler = new ListarFuncoesDuplicadasQueryHandler(db);
        var resultado = await handler.Handle(new ListarFuncoesDuplicadasQuery(), default);

        var grupo = Assert.Single(resultado);
        Assert.Equal(1, grupo.Manter.QuantidadeTrabalhadores);
        Assert.Equal(1, Assert.Single(grupo.Remover).QuantidadeTrabalhadores);
    }
}

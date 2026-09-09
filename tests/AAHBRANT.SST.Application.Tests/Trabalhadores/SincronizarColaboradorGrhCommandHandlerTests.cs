using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Application.Trabalhadores.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Trabalhadores;

public class SincronizarColaboradorGrhCommandHandlerTests
{
    private static readonly ICpfHashService CpfHash = new ICpfHashServiceFake();

    private static SincronizarColaboradorGrhCommand Comando(string cpf, string nome, string obraNome, string cargoNome) => new(
        Cpf: cpf,
        Nome: nome,
        Pis: "12345678900",
        Ctps: "1234567",
        DataNascimento: new DateTime(1990, 1, 1),
        NomeMae: "Maria da Silva",
        Endereco: "Rua das Flores, 251",
        Municipio: "São Paulo",
        Uf: "SP",
        Cep: "01000-000",
        Matricula: "MAT-001",
        DataAdmissao: new DateTime(2026, 8, 11),
        DataDemissao: null,
        CargoNome: cargoNome,
        CargoCboCodigo: "715125",
        Salario: 5400m,
        Situacao: SituacaoTrabalhador.Ativo,
        ObraNome: obraNome,
        DataFimExperiencia1: null,
        DataFimExperiencia2: null,
        TamanhoBlusaEpi: "G",
        TamanhoCalcaEpi: "42",
        TamanhoCalcadoEpi: "41");

    [Fact]
    public async Task Handle_TrabalhadorNovo_CriaComObraEFuncaoExistentes()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OBRA-1", Nome = "Ponte Rio Cuiá" };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        var handler = new SincronizarColaboradorGrhCommandHandler(db, CpfHash);
        var id = await handler.Handle(Comando("38062559890", "Adriano Manoel da Silva", "Ponte Rio Cuiá", "Operador De Maquina Nivel 3"), default);

        var trabalhador = await db.Trabalhadores.Include(t => t.Funcao).SingleAsync(t => t.Id == id);
        Assert.Equal("Adriano Manoel da Silva", trabalhador.Nome);
        Assert.Equal(obra.Id, trabalhador.ObraId);
        Assert.Equal("Operador De Maquina Nivel 3", trabalhador.Funcao!.Nome);
        Assert.Equal("715125", trabalhador.Funcao!.CboCodigo);
        Assert.Equal(5400m, trabalhador.Salario);
        Assert.Equal("G", trabalhador.TamanhoBlusaEpi);
        Assert.True(trabalhador.Ativo);
    }

    [Fact]
    public async Task Handle_ObraInexistente_TrabalhadorNovo_Falha()
    {
        var db = DbContextFactory.Criar();
        var handler = new SincronizarColaboradorGrhCommandHandler(db, CpfHash);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(Comando("38062559890", "Adriano Manoel da Silva", "Obra Que Não Existe", "Pedreiro"), default));
    }

    [Fact]
    public async Task Handle_TrabalhadorExistentePorCpf_AtualizaEmVezDeCriarDuplicado()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OBRA-1", Nome = "Ponte Rio Cuiá" };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        var handler = new SincronizarColaboradorGrhCommandHandler(db, CpfHash);
        var id1 = await handler.Handle(Comando("38062559890", "Adriano Manoel da Silva", "Ponte Rio Cuiá", "Operador De Maquina Nivel 3"), default);

        var id2 = await handler.Handle(Comando("38062559890", "Adriano M. da Silva", "Ponte Rio Cuiá", "Operador De Maquina Nivel 3"), default);

        Assert.Equal(id1, id2);
        var total = await db.Trabalhadores.CountAsync();
        Assert.Equal(1, total);
        var trabalhador = await db.Trabalhadores.SingleAsync();
        Assert.Equal("Adriano M. da Silva", trabalhador.Nome);
    }

    [Fact]
    public async Task Handle_CargoNaoExistente_CriaNovaFuncao()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OBRA-1", Nome = "Ponte Rio Cuiá" };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        var handler = new SincronizarColaboradorGrhCommandHandler(db, CpfHash);
        await handler.Handle(Comando("38062559890", "Adriano Manoel da Silva", "Ponte Rio Cuiá", "Cargo Novo Nunca Visto"), default);

        var funcao = await db.Funcoes.SingleAsync(f => f.Nome == "Cargo Novo Nunca Visto");
        Assert.Equal("715125", funcao.CboCodigo);
    }

    [Fact]
    public async Task Handle_SituacaoDesligado_MarcaTrabalhadorComoInativo()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OBRA-1", Nome = "Ponte Rio Cuiá" };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        var handler = new SincronizarColaboradorGrhCommandHandler(db, CpfHash);
        var comandoDesligado = Comando("38062559890", "Adriano Manoel da Silva", "Ponte Rio Cuiá", "Operador De Maquina Nivel 3")
            with { Situacao = SituacaoTrabalhador.Desligado };
        var id = await handler.Handle(comandoDesligado, default);

        var trabalhador = await db.Trabalhadores.IgnoreQueryFilters().SingleAsync(t => t.Id == id);
        Assert.False(trabalhador.Ativo);
        Assert.Equal(SituacaoTrabalhador.Desligado, trabalhador.Situacao);
    }

    [Fact]
    public async Task Handle_ComDataDemissao_PropagaParaTrabalhador()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OBRA-1", Nome = "Ponte Rio Cuiá" };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        var handler = new SincronizarColaboradorGrhCommandHandler(db, CpfHash);
        var comando = Comando("38062559890", "Adriano Manoel da Silva", "Ponte Rio Cuiá", "Operador De Maquina Nivel 3")
            with { DataDemissao = new DateTime(2026, 9, 1) };
        var id = await handler.Handle(comando, default);

        var trabalhador = await db.Trabalhadores.SingleAsync(t => t.Id == id);
        Assert.Equal(new DateTime(2026, 9, 1), trabalhador.DataDemissao);
    }

    [Fact]
    public async Task Handle_TrabalhadorSoftDeletadoLocalmente_ReativaEmVezDeDuplicar()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OBRA-1", Nome = "Ponte Rio Cuiá" };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        var handler = new SincronizarColaboradorGrhCommandHandler(db, CpfHash);
        var id = await handler.Handle(Comando("38062559890", "Adriano Manoel da Silva", "Ponte Rio Cuiá", "Operador De Maquina Nivel 3"), default);

        var trabalhador = await db.Trabalhadores.IgnoreQueryFilters().SingleAsync(t => t.Id == id);
        trabalhador.Ativo = false; // ex.: alguém excluiu manualmente pela tela
        await db.SaveChangesAsync();

        var idReativado = await handler.Handle(Comando("38062559890", "Adriano Manoel da Silva", "Ponte Rio Cuiá", "Operador De Maquina Nivel 3"), default);

        Assert.Equal(id, idReativado);
        var totalNaTabela = await db.Trabalhadores.IgnoreQueryFilters().CountAsync();
        Assert.Equal(1, totalNaTabela);
        var reativado = await db.Trabalhadores.SingleAsync(t => t.Id == id);
        Assert.True(reativado.Ativo);
    }
}

// Dublê fino: usa o mesmo CpfHashService real (chaves já configuradas pela DbContextFactory
// estática), só pra não expor o tipo concreto de Infrastructure como dependência direta do teste.
file class ICpfHashServiceFake : AAHBRANT.SST.Application.Common.Interfaces.ICpfHashService
{
    private readonly CpfHashService _real = new();
    public string CalcularHash(string cpfPlano) => _real.CalcularHash(cpfPlano);
}

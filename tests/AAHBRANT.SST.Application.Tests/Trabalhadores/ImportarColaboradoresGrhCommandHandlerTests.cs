using AAHBRANT.SST.Application;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Application.Trabalhadores.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AAHBRANT.SST.Application.Tests.Trabalhadores;

public class ImportarColaboradoresGrhCommandHandlerTests
{
    // Diferente de SincronizarColaboradorGrhCommandHandlerTests (chamado direto), aqui o handler
    // despacha um SincronizarColaboradorGrhCommand por colaborador via IMediator — precisa do pipeline
    // real (MediatR + ValidationBehavior) resolvido por DI, não só do handler isolado.
    private static IMediator CriarMediator(SstDbContext db)
    {
        var servicos = new ServiceCollection();
        servicos.AddLogging();
        servicos.AddApplication();
        servicos.AddSingleton<IAppDbContext>(db);
        servicos.AddSingleton<ICpfHashService>(new CpfHashServiceReal());
        return servicos.BuildServiceProvider().GetRequiredService<IMediator>();
    }

    private class CpfHashServiceReal : ICpfHashService
    {
        private readonly CpfHashService _real = new();
        public string CalcularHash(string cpfPlano) => _real.CalcularHash(cpfPlano);
    }

    private class ColaboradorGrhClientFake : IColaboradorGrhClient
    {
        private readonly IReadOnlyList<ColaboradorGrhDto> _colaboradores;
        public ColaboradorGrhClientFake(IReadOnlyList<ColaboradorGrhDto> colaboradores) => _colaboradores = colaboradores;
        public Task<IReadOnlyList<ColaboradorGrhDto>> ListarTodosAsync(CancellationToken ct = default)
            => Task.FromResult(_colaboradores);
    }

    private static ColaboradorGrhDto Colaborador(string cpf, string nome, string? obraNome = "Ponte Rio Cuiá", string? cargoNome = "Pedreiro") => new(
        Cpf: cpf,
        Nome: nome,
        Pis: null,
        Ctps: null,
        DataNascimento: null,
        NomeMae: null,
        Endereco: null,
        Municipio: null,
        Uf: null,
        Cep: null,
        Matricula: "MAT-1",
        DataAdmissao: new DateTime(2026, 1, 1),
        DataDemissao: null,
        Situacao: SituacaoTrabalhador.Ativo,
        Salario: null,
        DataFimExperiencia1: null,
        DataFimExperiencia2: null,
        TamanhoBlusaEpi: null,
        TamanhoCalcaEpi: null,
        TamanhoCalcadoEpi: null,
        ObraNome: obraNome,
        CargoNome: cargoNome,
        CargoCboCodigo: null);

    [Fact]
    public async Task Handle_ListaComDoisColaboradoresValidos_SincronizaOsDois()
    {
        var db = DbContextFactory.Criar();
        db.Obras.Add(new Obra { Codigo = "OBRA-1", Nome = "Ponte Rio Cuiá" });
        await db.SaveChangesAsync();

        var mediator = CriarMediator(db);
        var client = new ColaboradorGrhClientFake(new[]
        {
            Colaborador("38062559890", "Fulano de Tal"),
            Colaborador("52998224725", "Ciclano de Tal"),
        });
        var handler = new ImportarColaboradoresGrhCommandHandler(client, mediator);

        var resultado = await handler.Handle(new ImportarColaboradoresGrhCommand(), default);

        Assert.Equal(2, resultado.TotalRecebidos);
        Assert.Equal(2, resultado.TotalSincronizados);
        Assert.Empty(resultado.Erros);
        Assert.Equal(2, await db.Trabalhadores.CountAsync());
    }

    [Fact]
    public async Task Handle_ColaboradorComObraInexistente_RegistraErroSemAbortarLote()
    {
        var db = DbContextFactory.Criar();
        db.Obras.Add(new Obra { Codigo = "OBRA-1", Nome = "Ponte Rio Cuiá" });
        await db.SaveChangesAsync();

        var mediator = CriarMediator(db);
        var client = new ColaboradorGrhClientFake(new[]
        {
            Colaborador("38062559890", "Fulano de Tal", obraNome: "Obra Que Não Existe"),
            Colaborador("52998224725", "Ciclano de Tal"),
        });
        var handler = new ImportarColaboradoresGrhCommandHandler(client, mediator);

        var resultado = await handler.Handle(new ImportarColaboradoresGrhCommand(), default);

        Assert.Equal(2, resultado.TotalRecebidos);
        Assert.Equal(1, resultado.TotalSincronizados);
        var erro = Assert.Single(resultado.Erros);
        Assert.DoesNotContain("38062559890", erro); // CPF nunca em texto puro no relatório de erros
        Assert.Contains("***", erro);
        Assert.Equal(1, await db.Trabalhadores.CountAsync());
    }

    [Fact]
    public async Task Handle_ColaboradorSemCargo_RegistraErroSemAbortarLote()
    {
        var db = DbContextFactory.Criar();
        db.Obras.Add(new Obra { Codigo = "OBRA-1", Nome = "Ponte Rio Cuiá" });
        await db.SaveChangesAsync();

        var mediator = CriarMediator(db);
        var client = new ColaboradorGrhClientFake(new[] { Colaborador("38062559890", "Fulano de Tal", cargoNome: null) });
        var handler = new ImportarColaboradoresGrhCommandHandler(client, mediator);

        var resultado = await handler.Handle(new ImportarColaboradoresGrhCommand(), default);

        Assert.Equal(1, resultado.TotalRecebidos);
        Assert.Equal(0, resultado.TotalSincronizados);
        Assert.Single(resultado.Erros);
        Assert.Equal(0, await db.Trabalhadores.CountAsync());
    }
}

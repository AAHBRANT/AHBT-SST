using AAHBRANT.SST.Application.EntregasEpi.Commands;
using AAHBRANT.SST.Application.EntregasEpi.Queries;
using AAHBRANT.SST.Application.EntregasUniforme.Commands;
using AAHBRANT.SST.Application.EntregasUniforme.Queries;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Application.Treinamentos.Commands;
using AAHBRANT.SST.Application.Treinamentos.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Common;

// Auditoria de 09/10/2026, PR 2: treinamento e entregas de EPI/uniforme listavam a empresa inteira
// (sem obraId) e aceitavam editar/confirmar/excluir por id de outra obra.
public class EscopoObraTreinamentoEntregaTests
{
    static EscopoObraTreinamentoEntregaTests() => ChavesCpfDeTeste.Configurar();

    private static readonly Guid ObraA = Guid.NewGuid();
    private static readonly Guid ObraB = Guid.NewGuid();

    private sealed record Cenario(
        SstDbContext Global, SstDbContext Restrito,
        Treinamento TreinamentoA, Treinamento TreinamentoB,
        EntregaEpi EntregaEpiA, EntregaEpi EntregaEpiB,
        EntregaUniforme UniformeA, EntregaUniforme UniformeB);

    private static async Task<Cenario> CriarAsync()
    {
        var opcoes = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var global = new SstDbContext(opcoes, new CurrentUserService());
        var usuarioRestrito = new CurrentUserService();
        usuarioRestrito.DefinirEscopo(false, new[] { ObraA });
        var restrito = new SstDbContext(opcoes, usuarioRestrito);

        var funcao = new Funcao { Nome = "Pedreiro" };
        var deA = new Trabalhador { ObraId = ObraA, Funcao = funcao, Nome = "Trabalhador A", Cpf = "00000000001" };
        var deB = new Trabalhador { ObraId = ObraB, Funcao = funcao, Nome = "Trabalhador B", Cpf = "00000000002" };
        global.Funcoes.Add(funcao);
        global.Trabalhadores.AddRange(deA, deB);
        await global.SaveChangesAsync();

        var hoje = DateTime.UtcNow;
        Treinamento NovoTreinamento(Trabalhador t) => new() { TrabalhadorId = t.Id, CursoTreinamentoId = Guid.NewGuid(), DataRealizacao = hoje, DataValidade = hoje.AddYears(1) };
        EntregaEpi NovaEntregaEpi(Trabalhador t) => new() { TrabalhadorId = t.Id, CatalogoEpiId = Guid.NewGuid(), DataEntrega = hoje };
        EntregaUniforme NovoUniforme(Trabalhador t) => new() { TrabalhadorId = t.Id, CatalogoUniformeId = Guid.NewGuid(), Tamanho = "M", DataEntrega = hoje };

        var c = new Cenario(global, restrito,
            NovoTreinamento(deA), NovoTreinamento(deB),
            NovaEntregaEpi(deA), NovaEntregaEpi(deB),
            NovoUniforme(deA), NovoUniforme(deB));
        global.Treinamentos.AddRange(c.TreinamentoA, c.TreinamentoB);
        global.EntregasEpi.AddRange(c.EntregaEpiA, c.EntregaEpiB);
        global.EntregasUniforme.AddRange(c.UniformeA, c.UniformeB);
        await global.SaveChangesAsync();
        return c;
    }

    [Fact]
    public async Task Listagens_RestritoVeSoAPropriaObra_GlobalVeTudo()
    {
        var c = await CriarAsync();

        Assert.Equal(new[] { c.TreinamentoA.Id }, (await new ListarTreinamentosQueryHandler(c.Restrito).Handle(new ListarTreinamentosQuery(), default)).Select(x => x.Id));
        Assert.Equal(new[] { c.EntregaEpiA.Id }, (await new ListarEntregasEpiQueryHandler(c.Restrito).Handle(new ListarEntregasEpiQuery(), default)).Select(x => x.Id));
        Assert.Equal(new[] { c.UniformeA.Id }, (await new ListarEntregasUniformeQueryHandler(c.Restrito).Handle(new ListarEntregasUniformeQuery(null), default)).Select(x => x.Id));

        Assert.Equal(2, (await new ListarTreinamentosQueryHandler(c.Global).Handle(new ListarTreinamentosQuery(), default)).Count);
        Assert.Equal(2, (await new ListarEntregasEpiQueryHandler(c.Global).Handle(new ListarEntregasEpiQuery(), default)).Count);
    }

    [Fact]
    public async Task DetalheDeOutraObra_RetornaNulo()
    {
        var c = await CriarAsync();

        Assert.Null(await new ObterTreinamentoPorIdQueryHandler(c.Restrito).Handle(new ObterTreinamentoPorIdQuery(c.TreinamentoB.Id), default));
        Assert.Null(await new ObterEntregaEpiPorIdQueryHandler(c.Restrito).Handle(new ObterEntregaEpiPorIdQuery(c.EntregaEpiB.Id), default));
        Assert.NotNull(await new ObterTreinamentoPorIdQueryHandler(c.Restrito).Handle(new ObterTreinamentoPorIdQuery(c.TreinamentoA.Id), default));
    }

    [Fact]
    public async Task ConfirmarEExcluirDeOutraObra_Bloqueia()
    {
        var c = await CriarAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new ConfirmarEntregaEpiCommandHandler(c.Restrito).Handle(new ConfirmarEntregaEpiCommand(c.EntregaEpiB.Id), default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new ExcluirTreinamentoCommandHandler(c.Restrito).Handle(new ExcluirTreinamentoCommand(c.TreinamentoB.Id), default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new ExcluirEntregaEpiCommandHandler(c.Restrito).Handle(new ExcluirEntregaEpiCommand(c.EntregaEpiB.Id), default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new ExcluirEntregaUniformeCommandHandler(c.Restrito).Handle(new ExcluirEntregaUniformeCommand(c.UniformeB.Id), default));

        Assert.True(await c.Global.Treinamentos.AnyAsync(t => t.Id == c.TreinamentoB.Id));
    }
}

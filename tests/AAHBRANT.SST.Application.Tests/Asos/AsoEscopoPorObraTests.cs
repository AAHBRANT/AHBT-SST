using AAHBRANT.SST.Application.Asos.Commands;
using AAHBRANT.SST.Application.Asos.Queries;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Asos;

// Criar/Atualizar ASO só aceitam trabalhador dentro do escopo de obra do usuário. O filtro global de
// leitura do ASO foi revertido (escondia o ASO de trabalhadores desligados) — ver SstDbContext.
public class AsoEscopoPorObraTests
{
    static AsoEscopoPorObraTests() => ChavesCpfDeTeste.Configurar();

    private static readonly Guid ObraA = Guid.NewGuid();
    private static readonly Guid ObraB = Guid.NewGuid();

    // Mesma base InMemory, dois contextos: um global (para semear) e um restrito à obra A.
    private static (SstDbContext global, SstDbContext restrito) CriarContextos()
    {
        var nome = Guid.NewGuid().ToString();
        var opcoes = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nome).Options;
        var global = new SstDbContext(opcoes, new CurrentUserService());

        var usuarioRestrito = new CurrentUserService();
        usuarioRestrito.DefinirEscopo(false, new[] { ObraA });
        var restrito = new SstDbContext(opcoes, usuarioRestrito);
        return (global, restrito);
    }

    private static async Task<(Trabalhador deA, Trabalhador deB, Aso asoA, Aso asoB)> SemearAsync(SstDbContext db)
    {
        var funcao = new Funcao { Nome = "Pedreiro" };
        var deA = new Trabalhador { ObraId = ObraA, Funcao = funcao, Nome = "Trabalhador A", Cpf = "00000000001" };
        var deB = new Trabalhador { ObraId = ObraB, Funcao = funcao, Nome = "Trabalhador B", Cpf = "00000000002" };
        db.Funcoes.Add(funcao);
        db.Trabalhadores.AddRange(deA, deB);
        await db.SaveChangesAsync();

        var asoA = new Aso { TrabalhadorId = deA.Id, DataExame = DateTime.UtcNow, DataValidade = DateTime.UtcNow.AddYears(1), ObservacoesClinicas = "clinico A" };
        var asoB = new Aso { TrabalhadorId = deB.Id, DataExame = DateTime.UtcNow, DataValidade = DateTime.UtcNow.AddYears(1), ObservacoesClinicas = "clinico B" };
        db.Asos.AddRange(asoA, asoB);
        await db.SaveChangesAsync();
        return (deA, deB, asoA, asoB);
    }

    [Fact]
    public async Task Listar_AcessoGlobal_VeTodos()
    {
        var (global, _) = CriarContextos();
        await SemearAsync(global);

        var lista = await new ListarAsosQueryHandler(global).Handle(new ListarAsosQuery(), default);

        Assert.Equal(2, lista.Count);
    }

    [Fact]
    public async Task Criar_TrabalhadorDeOutraObra_Bloqueia()
    {
        var (global, restrito) = CriarContextos();
        var (_, deB, _, _) = await SemearAsync(global);
        var handler = new CriarAsoCommandHandler(restrito);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(
            new CriarAsoCommand(deB.Id, TipoExameAso.Periodico, DateTime.UtcNow, DateTime.UtcNow.AddYears(1),
                ResultadoAso.Apto, null, null, null), default));
    }

    [Fact]
    public async Task Criar_TrabalhadorInexistente_LancaKeyNotFoundEmVezDeErroDeFk()
    {
        var (_, restrito) = CriarContextos();
        var handler = new CriarAsoCommandHandler(restrito);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(
            new CriarAsoCommand(Guid.NewGuid(), TipoExameAso.Periodico, DateTime.UtcNow, DateTime.UtcNow.AddYears(1),
                ResultadoAso.Apto, null, null, null), default));
    }

    [Fact]
    public async Task Criar_TrabalhadorDaPropriaObra_Funciona()
    {
        var (global, restrito) = CriarContextos();
        var (deA, _, _, _) = await SemearAsync(global);
        var handler = new CriarAsoCommandHandler(restrito);

        var id = await handler.Handle(
            new CriarAsoCommand(deA.Id, TipoExameAso.Periodico, DateTime.UtcNow, DateTime.UtcNow.AddYears(1),
                ResultadoAso.Apto, null, null, null), default);

        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public async Task Atualizar_MoverAsoParaTrabalhadorDeOutraObra_Bloqueia()
    {
        var (global, restrito) = CriarContextos();
        var (_, deB, asoA, _) = await SemearAsync(global);
        var handler = new AtualizarAsoCommandHandler(restrito);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(
            new AtualizarAsoCommand(asoA.Id, deB.Id, TipoExameAso.Periodico, DateTime.UtcNow, DateTime.UtcNow.AddYears(1),
                ResultadoAso.Apto, null, null, null), default));
    }

    // Guarda de regressão: ASO de trabalhador desligado (Ativo = false) continua consultável.
    [Fact]
    public async Task Listar_AsoDeTrabalhadorDesligado_ContinuaVisivel()
    {
        var (global, _) = CriarContextos();
        var (deA, _, _, _) = await SemearAsync(global);
        var trabalhador = await global.Trabalhadores.FirstAsync(t => t.Id == deA.Id);
        trabalhador.Ativo = false;
        await global.SaveChangesAsync();

        var lista = await new ListarAsosQueryHandler(global).Handle(new ListarAsosQuery(), default);

        Assert.Equal(2, lista.Count);
    }
}

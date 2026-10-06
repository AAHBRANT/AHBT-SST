using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Common;

// Auditoria 06/10/2026 (A2), continuação do ASO: exames, aptidões, treinamentos, turmas, contratos e
// estoques/instalações de EPC e uniforme passam a respeitar o escopo de obra do usuário.
public class EscopoPorObraDemaisRegistrosTests
{
    static EscopoPorObraDemaisRegistrosTests() => ChavesCpfDeTeste.Configurar();

    private static readonly Guid ObraA = Guid.NewGuid();
    private static readonly Guid ObraB = Guid.NewGuid();

    private static (SstDbContext global, SstDbContext restrito) CriarContextos()
    {
        var opcoes = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var global = new SstDbContext(opcoes, new CurrentUserService());
        var usuarioRestrito = new CurrentUserService();
        usuarioRestrito.DefinirEscopo(false, new[] { ObraA });
        return (global, new SstDbContext(opcoes, usuarioRestrito));
    }

    private static async Task<(Trabalhador deA, Trabalhador deB)> SemearTrabalhadoresAsync(SstDbContext db)
    {
        var funcao = new Funcao { Nome = "Pedreiro" };
        var deA = new Trabalhador { ObraId = ObraA, Funcao = funcao, Nome = "A", Cpf = "00000000001" };
        var deB = new Trabalhador { ObraId = ObraB, Funcao = funcao, Nome = "B", Cpf = "00000000002" };
        db.Funcoes.Add(funcao);
        db.Trabalhadores.AddRange(deA, deB);
        await db.SaveChangesAsync();
        return (deA, deB);
    }

    [Fact]
    public async Task ExameComplementar_PerfilRestrito_VeSoDaPropriaObra()
    {
        var (global, restrito) = CriarContextos();
        var (deA, deB) = await SemearTrabalhadoresAsync(global);
        global.ExamesComplementares.AddRange(
            new ExameComplementar { TrabalhadorId = deA.Id, Resultado = "A" },
            new ExameComplementar { TrabalhadorId = deB.Id, Resultado = "B" });
        await global.SaveChangesAsync();

        Assert.Equal(2, await global.ExamesComplementares.CountAsync());
        var visivel = Assert.Single(await restrito.ExamesComplementares.ToListAsync());
        Assert.Equal("A", visivel.Resultado);
    }

    [Fact]
    public async Task Aptidao_PerfilRestrito_VeSoDaPropriaObra()
    {
        var (global, restrito) = CriarContextos();
        var (deA, deB) = await SemearTrabalhadoresAsync(global);
        global.AptidoesAtividadeEspecifica.AddRange(
            new AptidaoAtividadeEspecifica { TrabalhadorId = deA.Id, AtividadeCritica = "Altura A" },
            new AptidaoAtividadeEspecifica { TrabalhadorId = deB.Id, AtividadeCritica = "Altura B" });
        await global.SaveChangesAsync();

        Assert.Equal(2, await global.AptidoesAtividadeEspecifica.CountAsync());
        var visivel = Assert.Single(await restrito.AptidoesAtividadeEspecifica.ToListAsync());
        Assert.Equal("Altura A", visivel.AtividadeCritica);
    }

    [Fact]
    public async Task Treinamento_PerfilRestrito_VeSoDaPropriaObra()
    {
        var (global, restrito) = CriarContextos();
        var (deA, deB) = await SemearTrabalhadoresAsync(global);
        var curso = new CursoTreinamento { Nome = "NR-35" };
        global.CursosTreinamento.Add(curso);
        await global.SaveChangesAsync();
        global.Treinamentos.AddRange(
            new Treinamento { TrabalhadorId = deA.Id, CursoTreinamentoId = curso.Id, NumeroCertificado = "A" },
            new Treinamento { TrabalhadorId = deB.Id, CursoTreinamentoId = curso.Id, NumeroCertificado = "B" });
        await global.SaveChangesAsync();

        Assert.Equal(2, await global.Treinamentos.CountAsync());
        var visivel = Assert.Single(await restrito.Treinamentos.ToListAsync());
        Assert.Equal("A", visivel.NumeroCertificado);
    }

    [Fact]
    public async Task SessaoTreinamento_PerfilRestrito_VeSoDaPropriaObra()
    {
        var (global, restrito) = CriarContextos();
        global.SessoesTreinamento.AddRange(
            new SessaoTreinamento { ObraId = ObraA, NumeroCertificado = "A" },
            new SessaoTreinamento { ObraId = ObraB, NumeroCertificado = "B" });
        await global.SaveChangesAsync();

        Assert.Equal(2, await global.SessoesTreinamento.CountAsync());
        var visivel = Assert.Single(await restrito.SessoesTreinamento.ToListAsync());
        Assert.Equal("A", visivel.NumeroCertificado);
    }

    [Fact]
    public async Task Contrato_PerfilRestrito_VeSoDaPropriaObra()
    {
        var (global, restrito) = CriarContextos();
        global.Contratos.AddRange(
            new Contrato { ObraId = ObraA, NumeroContrato = "A" },
            new Contrato { ObraId = ObraB, NumeroContrato = "B" });
        await global.SaveChangesAsync();

        Assert.Equal(2, await global.Contratos.CountAsync());
        var visivel = Assert.Single(await restrito.Contratos.ToListAsync());
        Assert.Equal("A", visivel.NumeroContrato);
    }

    [Fact]
    public async Task EstoqueEpc_InstalacaoEpc_EstoqueUniforme_PerfilRestrito_VeSoDaPropriaObra()
    {
        var (global, restrito) = CriarContextos();
        global.EstoquesEpc.AddRange(new EstoqueEpc { ObraId = ObraA }, new EstoqueEpc { ObraId = ObraB });
        global.InstalacoesEpc.AddRange(new InstalacaoEpc { ObraId = ObraA }, new InstalacaoEpc { ObraId = ObraB });
        global.EstoquesUniforme.AddRange(new EstoqueUniforme { ObraId = ObraA }, new EstoqueUniforme { ObraId = ObraB });
        await global.SaveChangesAsync();

        Assert.Equal(2, await global.EstoquesEpc.CountAsync());
        Assert.Equal(2, await global.InstalacoesEpc.CountAsync());
        Assert.Equal(2, await global.EstoquesUniforme.CountAsync());
        Assert.Equal(ObraA, Assert.Single(await restrito.EstoquesEpc.ToListAsync()).ObraId);
        Assert.Equal(ObraA, Assert.Single(await restrito.InstalacoesEpc.ToListAsync()).ObraId);
        Assert.Equal(ObraA, Assert.Single(await restrito.EstoquesUniforme.ToListAsync()).ObraId);
    }
}

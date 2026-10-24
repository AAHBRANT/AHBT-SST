using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Common;

// Auditoria 06/10/2026 (A2): turmas, contratos e estoques/instalações de EPC e uniforme (todos com
// ObraId próprio) passam a respeitar o escopo de obra do usuário.
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

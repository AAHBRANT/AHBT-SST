using AAHBRANT.SST.Application.AcoesPlano.Queries;
using AAHBRANT.SST.Application.Equipes.Queries;
using AAHBRANT.SST.Application.NaoConformidades.Commands;
using AAHBRANT.SST.Application.NaoConformidades.Queries;
using AAHBRANT.SST.Application.PlanoAcao.Queries;
using AAHBRANT.SST.Application.Riscos.Commands;
using AAHBRANT.SST.Application.Riscos.Queries;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Common;

// Auditoria de 09/10/2026, PR 3: não conformidade, risco, plano de ação do PGR, ação de plano e
// equipes não tinham escopo por obra — listavam e alteravam registro de qualquer obra.
public class EscopoObraDemaisRegistrosRestritoTests
{
    static EscopoObraDemaisRegistrosRestritoTests() => ChavesCpfDeTeste.Configurar();

    private sealed record Cenario(
        SstDbContext Global, SstDbContext Restrito,
        NaoConformidade NcA, NaoConformidade NcB, NaoConformidade NcSemVinculo,
        Risco RiscoA, Risco RiscoB, Pgr PgrB, Equipe EquipeA);

    private static async Task<Cenario> CriarAsync()
    {
        var opcoes = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var global = new SstDbContext(opcoes, new CurrentUserService());

        var obraA = new Obra { Nome = "Obra A" };
        var obraB = new Obra { Nome = "Obra B" };
        global.Obras.AddRange(obraA, obraB);
        var atividadeA = new Atividade { ObraId = obraA.Id, Nome = "Alvenaria A" };
        var atividadeB = new Atividade { ObraId = obraB.Id, Nome = "Alvenaria B" };
        var perigo = new Perigo { Nome = "Queda" };
        global.Atividades.AddRange(atividadeA, atividadeB);
        global.Perigos.Add(perigo);
        var riscoA = new Risco { AtividadeId = atividadeA.Id, PerigoId = perigo.Id };
        var riscoB = new Risco { AtividadeId = atividadeB.Id, PerigoId = perigo.Id };
        global.Riscos.AddRange(riscoA, riscoB);
        var ncA = new NaoConformidade { Descricao = "NC A", AtividadeId = atividadeA.Id };
        var ncB = new NaoConformidade { Descricao = "NC B", AtividadeId = atividadeB.Id };
        var ncSemVinculo = new NaoConformidade { Descricao = "NC sem atividade" };
        global.NaoConformidades.AddRange(ncA, ncB, ncSemVinculo);
        var pgrB = new Pgr { ObraId = obraB.Id, Nome = "PGR B" };
        global.Pgrs.Add(pgrB);
        global.PlanoAcaoItens.Add(new PlanoAcaoItem { PgrId = pgrB.Id, Descricao = "Item B" });
        global.AcoesPlano.Add(new AcaoPlano { OrigemTipo = nameof(NaoConformidade), OrigemId = ncB.Id, Descricao = "Ação B" });
        var setorA = new Setor { ObraId = obraA.Id, Nome = "Geral" };
        var setorB = new Setor { ObraId = obraB.Id, Nome = "Geral" };
        global.Setores.AddRange(setorA, setorB);
        var equipeA = new Equipe { SetorId = setorA.Id, Nome = "Equipe A" };
        global.Equipes.AddRange(equipeA, new Equipe { SetorId = setorB.Id, Nome = "Equipe B" });
        await global.SaveChangesAsync();

        var usuarioRestrito = new CurrentUserService();
        usuarioRestrito.DefinirEscopo(false, new[] { obraA.Id });
        var restrito = new SstDbContext(opcoes, usuarioRestrito);
        return new Cenario(global, restrito, ncA, ncB, ncSemVinculo, riscoA, riscoB, pgrB, equipeA);
    }

    [Fact]
    public async Task NaoConformidade_Listar_RestritoVeAPropriaObraEAsSemVinculo()
    {
        var c = await CriarAsync();

        var restrito = await new ListarNaoConformidadesQueryHandler(c.Restrito).Handle(new ListarNaoConformidadesQuery(null), default);
        var global = await new ListarNaoConformidadesQueryHandler(c.Global).Handle(new ListarNaoConformidadesQuery(null), default);

        Assert.Equal(
            new[] { c.NcA.Id, c.NcSemVinculo.Id }.OrderBy(x => x),
            restrito.Select(n => n.Id).OrderBy(x => x));
        Assert.Equal(3, global.Count);
    }

    [Fact]
    public async Task NaoConformidade_ExcluirDeOutraObra_Bloqueia()
    {
        var c = await CriarAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new ExcluirNaoConformidadeCommandHandler(c.Restrito).Handle(new ExcluirNaoConformidadeCommand(c.NcB.Id), default));
    }

    [Fact]
    public async Task Risco_ListarEExcluir_RestritoSoNaPropriaObra()
    {
        var c = await CriarAsync();

        var lista = await new ListarRiscosQueryHandler(c.Restrito).Handle(new ListarRiscosQuery(), default);
        Assert.Equal(new[] { c.RiscoA.Id }, lista.Select(r => r.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new ExcluirRiscoCommandHandler(c.Restrito).Handle(new ExcluirRiscoCommand(c.RiscoB.Id), default));
    }

    [Fact]
    public async Task PlanoAcaoDoPgrEAcaoDeNc_DeOutraObra_NaoAparecem()
    {
        var c = await CriarAsync();

        Assert.Empty(await new ListarPlanoAcaoItensQueryHandler(c.Restrito).Handle(new ListarPlanoAcaoItensQuery(c.PgrB.Id), default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new ListarAcoesPlanoQueryHandler(c.Restrito).Handle(new ListarAcoesPlanoQuery(nameof(NaoConformidade), c.NcB.Id), default));
        Assert.Single(await new ListarAcoesPlanoQueryHandler(c.Global).Handle(new ListarAcoesPlanoQuery(nameof(NaoConformidade), c.NcB.Id), default));
    }

    [Fact]
    public async Task Equipes_Listar_RestritoVeSoAPropriaObra()
    {
        var c = await CriarAsync();

        var lista = await new ListarEquipesQueryHandler(c.Restrito).Handle(new ListarEquipesQuery(null, null), default);

        Assert.Equal(new[] { c.EquipeA.Id }, lista.Select(e => e.Id));
    }
}

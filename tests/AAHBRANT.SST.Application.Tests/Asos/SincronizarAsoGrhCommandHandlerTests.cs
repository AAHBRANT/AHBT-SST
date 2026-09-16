using AAHBRANT.SST.Application.Asos.Commands;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Asos;

public class SincronizarAsoGrhCommandHandlerTests
{
    private static readonly ICpfHashService CpfHash = new ICpfHashServiceFake();

    private static async Task<Trabalhador> CriarTrabalhadorAsync(AAHBRANT.SST.Infrastructure.Persistencia.SstDbContext db, string cpf)
    {
        var obra = new Obra { Codigo = "OBRA-1", Nome = "Ponte Rio Cuiá" };
        db.Obras.Add(obra);
        var trabalhador = new Trabalhador
        {
            ObraId = obra.Id,
            Nome = "Adriano Manoel da Silva",
            Cpf = cpf,
            DataAdmissao = new DateTime(2026, 1, 1),
        };
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();
        return trabalhador;
    }

    private static SincronizarAsoGrhCommand Comando(
        string grhId, string cpf, DateTime? validade, string? aptidao = null, string? restricao = null, string? medico = null) => new(
        GrhAsoId: grhId,
        Cpf: cpf,
        DataExame: new DateTime(2026, 6, 25),
        DataValidade: validade,
        Aptidao: aptidao,
        RestricaoClinica: restricao,
        MedicoNome: medico);

    [Fact]
    public async Task Handle_ComValidade_CriaAsoNovo()
    {
        var db = DbContextFactory.Criar();
        var trabalhador = await CriarTrabalhadorAsync(db, "38062559890");

        var handler = new SincronizarAsoGrhCommandHandler(db, CpfHash);
        var id = await handler.Handle(
            Comando("GRH-ASO-001", "38062559890", new DateTime(2027, 6, 25), aptidao: "APTO"), default);

        var aso = await db.Asos.SingleAsync(a => a.Id == id);
        Assert.Equal(trabalhador.Id, aso.TrabalhadorId);
        Assert.Equal("GRH-ASO-001", aso.GrhAsoId);
        Assert.Equal(new DateTime(2027, 6, 25), aso.DataValidade);
        Assert.Equal(ResultadoAso.Apto, aso.ResultadoStatus);
        Assert.NotNull(aso.DataUltimaSincronizacao);
    }

    [Fact]
    public async Task Handle_SemValidade_RejeitaENaoCria()
    {
        var db = DbContextFactory.Criar();
        await CriarTrabalhadorAsync(db, "38062559890");

        var handler = new SincronizarAsoGrhCommandHandler(db, CpfHash);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(Comando("GRH-ASO-001", "38062559890", validade: null), default));

        Assert.False(await db.Asos.AnyAsync());
    }

    [Fact]
    public async Task Handle_TrabalhadorNaoEncontrado_Falha()
    {
        var db = DbContextFactory.Criar();
        var handler = new SincronizarAsoGrhCommandHandler(db, CpfHash);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(Comando("GRH-ASO-001", "38062559890", new DateTime(2027, 6, 25)), default));
    }

    [Fact]
    public async Task Handle_AptidaoNulaNaAtualizacao_NaoApagaResultadoJaLancadoNoSst()
    {
        var db = DbContextFactory.Criar();
        await CriarTrabalhadorAsync(db, "38062559890");

        var handler = new SincronizarAsoGrhCommandHandler(db, CpfHash);
        var id = await handler.Handle(
            Comando("GRH-ASO-001", "38062559890", new DateTime(2027, 6, 25), aptidao: "APTO"), default);

        // médico do trabalho ajusta manualmente no SST depois da primeira sincronização
        var aso = await db.Asos.SingleAsync(a => a.Id == id);
        aso.ResultadoStatus = ResultadoAso.AptoComRestricao;
        await db.SaveChangesAsync();

        // rodada seguinte do G-RH chega sem aptidao preenchida (comportamento real observado)
        await handler.Handle(Comando("GRH-ASO-001", "38062559890", new DateTime(2027, 6, 25), aptidao: null), default);

        var asoFinal = await db.Asos.SingleAsync(a => a.Id == id);
        Assert.Equal(ResultadoAso.AptoComRestricao, asoFinal.ResultadoStatus);
    }

    [Fact]
    public async Task Handle_ExisteRegistro_AtualizaEmVezDeCriarDuplicado()
    {
        var db = DbContextFactory.Criar();
        await CriarTrabalhadorAsync(db, "38062559890");

        var handler = new SincronizarAsoGrhCommandHandler(db, CpfHash);
        var id1 = await handler.Handle(
            Comando("GRH-ASO-001", "38062559890", new DateTime(2027, 6, 25)), default);
        var id2 = await handler.Handle(
            Comando("GRH-ASO-001", "38062559890", new DateTime(2027, 7, 1)), default);

        Assert.Equal(id1, id2);
        Assert.Equal(1, await db.Asos.CountAsync());
        var aso = await db.Asos.SingleAsync();
        Assert.Equal(new DateTime(2027, 7, 1), aso.DataValidade);
    }

    [Fact]
    public async Task Handle_ComRestricaoClinica_AdicionaSemDuplicar()
    {
        var db = DbContextFactory.Criar();
        await CriarTrabalhadorAsync(db, "38062559890");

        var handler = new SincronizarAsoGrhCommandHandler(db, CpfHash);
        var id = await handler.Handle(
            Comando("GRH-ASO-001", "38062559890", new DateTime(2027, 6, 25), restricao: "Não operar em altura"), default);

        await handler.Handle(
            Comando("GRH-ASO-001", "38062559890", new DateTime(2027, 6, 25), restricao: "Não operar em altura"), default);

        var aso = await db.Asos.Include(a => a.Restricoes).SingleAsync(a => a.Id == id);
        Assert.Single(aso.Restricoes);
        Assert.Equal("Não operar em altura", aso.Restricoes.Single().Descricao);
    }
}

// Dublê fino: usa o mesmo CpfHashService real (chaves já configuradas pela DbContextFactory
// estática) — mesmo padrão de SincronizarColaboradorGrhCommandHandlerTests/SincronizarAlojamentoGrhCommandHandlerTests.
file class ICpfHashServiceFake : ICpfHashService
{
    private readonly CpfHashService _real = new();
    public string CalcularHash(string cpfPlano) => _real.CalcularHash(cpfPlano);
}

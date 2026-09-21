using AAHBRANT.SST.Application.Alojamentos.Commands;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Alojamentos;

public class SincronizarAlojamentoGrhCommandHandlerTests
{
    private static readonly ICpfHashService CpfHash = new ICpfHashServiceFake();

    private static SincronizarAlojamentoGrhCommand Comando(
        string grhId, string nome, string? obraNome, params AlojamentoMoradorGrhDto[] moradores) => new(
        GrhAlojamentoId: grhId,
        Nome: nome,
        ObraNome: obraNome,
        Endereco: "Rua das Flores, 251",
        Ativo: true,
        Moradores: moradores);

    [Fact]
    public async Task Handle_ObraValida_CriaAlojamentoNovo()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OBRA-1", Nome = "Ponte Rio Cuiá" };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        var handler = new SincronizarAlojamentoGrhCommandHandler(db, CpfHash);
        var id = await handler.Handle(Comando("GRH-001", "Alojamento 01", "Ponte Rio Cuiá"), default);

        var alojamento = await db.Alojamentos.SingleAsync(a => a.Id == id);
        Assert.Equal("Alojamento 01", alojamento.Nome);
        Assert.Equal("GRH-001", alojamento.GrhAlojamentoId);
        Assert.Equal(obra.Id, alojamento.ObraId);
        Assert.NotNull(alojamento.DataUltimaSincronizacao);
        Assert.True(alojamento.Ativo);
    }

    [Fact]
    public async Task Handle_ObraInexistente_AlojamentoNovo_Falha()
    {
        var db = DbContextFactory.Criar();
        var handler = new SincronizarAlojamentoGrhCommandHandler(db, CpfHash);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(Comando("GRH-001", "Alojamento 01", "Obra Que Não Existe"), default));
    }

    [Fact]
    public async Task Handle_AlojamentoExistentePorGrhId_AtualizaEmVezDeCriarDuplicado()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OBRA-1", Nome = "Ponte Rio Cuiá" };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        var handler = new SincronizarAlojamentoGrhCommandHandler(db, CpfHash);
        var id1 = await handler.Handle(Comando("GRH-001", "Alojamento 01", "Ponte Rio Cuiá"), default);
        var id2 = await handler.Handle(Comando("GRH-001", "Alojamento 01 (renomeado)", "Ponte Rio Cuiá"), default);

        Assert.Equal(id1, id2);
        Assert.Equal(1, await db.Alojamentos.CountAsync());
        var alojamento = await db.Alojamentos.SingleAsync();
        Assert.Equal("Alojamento 01 (renomeado)", alojamento.Nome);
    }

    [Fact]
    public async Task Handle_AlojamentoSoftDeletadoLocalmente_ReativaEmVezDeDuplicar()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OBRA-1", Nome = "Ponte Rio Cuiá" };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        var handler = new SincronizarAlojamentoGrhCommandHandler(db, CpfHash);
        var id = await handler.Handle(Comando("GRH-001", "Alojamento 01", "Ponte Rio Cuiá"), default);

        var alojamento = await db.Alojamentos.IgnoreQueryFilters().SingleAsync(a => a.Id == id);
        alojamento.Ativo = false; // ex.: alguém excluiu manualmente pela tela
        await db.SaveChangesAsync();

        var idReativado = await handler.Handle(Comando("GRH-001", "Alojamento 01", "Ponte Rio Cuiá"), default);

        Assert.Equal(id, idReativado);
        Assert.Equal(1, await db.Alojamentos.IgnoreQueryFilters().CountAsync());
        var reativado = await db.Alojamentos.SingleAsync(a => a.Id == id);
        Assert.True(reativado.Ativo);
    }

    [Fact]
    public async Task Handle_GrhMarcaComoInativo_DesativaNoSst()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OBRA-1", Nome = "Ponte Rio Cuiá" };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        var handler = new SincronizarAlojamentoGrhCommandHandler(db, CpfHash);
        var id = await handler.Handle(Comando("GRH-001", "Alojamento 01", "Ponte Rio Cuiá"), default);

        var comandoInativo = Comando("GRH-001", "Alojamento 01", "Ponte Rio Cuiá") with { Ativo = false };
        await handler.Handle(comandoInativo, default);

        var alojamento = await db.Alojamentos.IgnoreQueryFilters().SingleAsync(a => a.Id == id);
        Assert.False(alojamento.Ativo);
    }

    [Fact]
    public async Task Handle_MoradorNovo_CriaVinculoAtivo()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OBRA-1", Nome = "Ponte Rio Cuiá" };
        db.Obras.Add(obra);
        var trabalhador = new Trabalhador
        {
            ObraId = obra.Id,
            Nome = "Adriano Manoel da Silva",
            Cpf = "38062559890",
            DataAdmissao = new DateTime(2026, 1, 1),
        };
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();

        var handler = new SincronizarAlojamentoGrhCommandHandler(db, CpfHash);
        var desde = new DateTime(2026, 9, 1);
        var id = await handler.Handle(
            Comando("GRH-001", "Alojamento 01", "Ponte Rio Cuiá",
                new AlojamentoMoradorGrhDto("38062559890", "MAT-001", desde)),
            default);

        var vinculo = await db.AlojamentoMoradores.SingleAsync(m => m.AlojamentoId == id);
        Assert.Equal(trabalhador.Id, vinculo.TrabalhadorId);
        Assert.Equal(desde, vinculo.DataDesde);
        Assert.Null(vinculo.DataSaida);
    }

    [Fact]
    public async Task Handle_MoradorSaiuDaListaDoGrh_FechaVinculo()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OBRA-1", Nome = "Ponte Rio Cuiá" };
        db.Obras.Add(obra);
        var trabalhador = new Trabalhador
        {
            ObraId = obra.Id,
            Nome = "Adriano Manoel da Silva",
            Cpf = "38062559890",
            DataAdmissao = new DateTime(2026, 1, 1),
        };
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();

        var handler = new SincronizarAlojamentoGrhCommandHandler(db, CpfHash);
        var id = await handler.Handle(
            Comando("GRH-001", "Alojamento 01", "Ponte Rio Cuiá",
                new AlojamentoMoradorGrhDto("38062559890", "MAT-001", new DateTime(2026, 9, 1))),
            default);

        // segunda sincronização sem o morador na lista — G-RH manda a lista completa e atual
        await handler.Handle(Comando("GRH-001", "Alojamento 01", "Ponte Rio Cuiá"), default);

        var vinculo = await db.AlojamentoMoradores.SingleAsync(m => m.AlojamentoId == id);
        Assert.NotNull(vinculo.DataSaida);
    }

    [Fact]
    public async Task Handle_MoradorMudouDeAlojamento_FechaVinculoAntigoEAbreNovo()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OBRA-1", Nome = "Ponte Rio Cuiá" };
        db.Obras.Add(obra);
        var trabalhador = new Trabalhador
        {
            ObraId = obra.Id,
            Nome = "Adriano Manoel da Silva",
            Cpf = "38062559890",
            DataAdmissao = new DateTime(2026, 1, 1),
        };
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();

        var handler = new SincronizarAlojamentoGrhCommandHandler(db, CpfHash);
        var idA = await handler.Handle(
            Comando("GRH-A", "Alojamento A", "Ponte Rio Cuiá",
                new AlojamentoMoradorGrhDto("38062559890", "MAT-001", new DateTime(2026, 8, 1))),
            default);

        var novaData = new DateTime(2026, 9, 10);
        var idB = await handler.Handle(
            Comando("GRH-B", "Alojamento B", "Ponte Rio Cuiá",
                new AlojamentoMoradorGrhDto("38062559890", "MAT-001", novaData)),
            default);

        var vinculoA = await db.AlojamentoMoradores.SingleAsync(m => m.AlojamentoId == idA);
        Assert.NotNull(vinculoA.DataSaida);

        var vinculoB = await db.AlojamentoMoradores.SingleAsync(m => m.AlojamentoId == idB);
        Assert.Null(vinculoB.DataSaida);
        Assert.Equal(novaData, vinculoB.DataDesde);
    }

    [Fact]
    public async Task Handle_MoradorNaoEncontradoPorCpf_PuloSemFalharOAlojamento()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OBRA-1", Nome = "Ponte Rio Cuiá" };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        var handler = new SincronizarAlojamentoGrhCommandHandler(db, CpfHash);
        var id = await handler.Handle(
            Comando("GRH-001", "Alojamento 01", "Ponte Rio Cuiá",
                new AlojamentoMoradorGrhDto("00000000000", "MAT-999", new DateTime(2026, 9, 1))),
            default);

        var alojamento = await db.Alojamentos.SingleAsync(a => a.Id == id);
        Assert.Equal("Alojamento 01", alojamento.Nome);
        Assert.False(await db.AlojamentoMoradores.AnyAsync());
    }

    // Bug real encontrado em homologação (21/09): a obra do alojamento mudou numa re-sincronização
    // e as Inspeções já criadas ficaram com o ObraId antigo, escondidas pelo filtro de escopo por
    // obra (SstDbContext) de quem só tinha acesso à obra nova — "Continuar inspeção" dava 404 só
    // nos alojamentos com inspeção em aberto criada antes da mudança.
    [Fact]
    public async Task Handle_ObraDoAlojamentoMuda_PropagaNovaObraParaInspecoesExistentes()
    {
        var db = DbContextFactory.Criar();
        var obraAntiga = new Obra { Codigo = "OBRA-1", Nome = "Ponte Rio Cuiá" };
        var obraNova = new Obra { Codigo = "OBRA-2", Nome = "Consórcio Ponte Rio Cuiá" };
        db.Obras.AddRange(obraAntiga, obraNova);
        await db.SaveChangesAsync();

        var handler = new SincronizarAlojamentoGrhCommandHandler(db, CpfHash);
        var alojamentoId = await handler.Handle(Comando("GRH-001", "Alojamento 01", "Ponte Rio Cuiá"), default);

        var inspecaoEmAndamento = new Inspecao
        {
            TipoInspecao = TipoInspecao.Alojamento,
            ObraId = obraAntiga.Id,
            AlojamentoId = alojamentoId,
            ChecklistModeloId = Guid.NewGuid(),
            ResponsavelUsuarioId = Guid.NewGuid(),
            Data = new DateTime(2026, 9, 1),
            Status = StatusInspecao.EmAndamento,
        };
        db.Inspecoes.Add(inspecaoEmAndamento);
        await db.SaveChangesAsync();

        // Re-sincroniza o mesmo alojamento (mesmo GrhAlojamentoId), agora resolvendo para a obra nova.
        await handler.Handle(Comando("GRH-001", "Alojamento 01", "Consórcio Ponte Rio Cuiá"), default);

        var alojamento = await db.Alojamentos.SingleAsync(a => a.Id == alojamentoId);
        Assert.Equal(obraNova.Id, alojamento.ObraId);

        var inspecaoAtualizada = await db.Inspecoes.IgnoreQueryFilters().SingleAsync(i => i.Id == inspecaoEmAndamento.Id);
        Assert.Equal(obraNova.Id, inspecaoAtualizada.ObraId);
    }

    [Fact]
    public async Task Handle_TrabalhadorDesligado_NaoEVinculado()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OBRA-1", Nome = "Ponte Rio Cuiá" };
        db.Obras.Add(obra);
        var trabalhador = new Trabalhador
        {
            ObraId = obra.Id,
            Nome = "Adriano Manoel da Silva",
            Cpf = "38062559890",
            DataAdmissao = new DateTime(2026, 1, 1),
            Ativo = false,
        };
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();

        var handler = new SincronizarAlojamentoGrhCommandHandler(db, CpfHash);
        await handler.Handle(
            Comando("GRH-001", "Alojamento 01", "Ponte Rio Cuiá",
                new AlojamentoMoradorGrhDto("38062559890", "MAT-001", new DateTime(2026, 9, 1))),
            default);

        Assert.False(await db.AlojamentoMoradores.AnyAsync());
    }
}

// Dublê fino: usa o mesmo CpfHashService real (chaves já configuradas pela DbContextFactory
// estática) — mesmo padrão de SincronizarColaboradorGrhCommandHandlerTests.
file class ICpfHashServiceFake : ICpfHashService
{
    private readonly CpfHashService _real = new();
    public string CalcularHash(string cpfPlano) => _real.CalcularHash(cpfPlano);
}

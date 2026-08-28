using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Integracao.Bot;
using AAHBRANT.SST.Infrastructure.Persistencia;`nusing AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Alertas;

// Cobre AAHBRANT.SST.Infrastructure.Integracao.Bot.CalendarioTeamsMensagemHandler — a lógica de
// estado compartilhada pelos dois consumidores da fila de calendário (docs/superpowers/specs/
// 2026-08-28-calendario-teams-design.md §4.3/§5). Acessível aqui porque é internal + InternalsVisibleTo
// (ver AAHBRANT.SST.Infrastructure/AssemblyInfo.cs) — sem isso não haveria como testar sem duplicar a
// lógica ou usar reflection.
public class CalendarioTeamsMensagemHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco) =>
        new SstDbContext(new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nomeBanco).Options, new CurrentUserService());

    private static CalendarioTeamsMensagem NovaMensagem(
        OperacaoCalendarioTeams operacao, Guid entidadeOrigemId, Guid organizadorUsuarioId,
        string? titulo = "Título", string? descricao = "Descrição", DateTime? data = null) =>
        new("Alerta", entidadeOrigemId, operacao, organizadorUsuarioId, titulo, descricao, data ?? DateTime.UtcNow.Date);

    [Fact]
    public async Task ProcessarAsync_Criar_SemRegistroExistente_CriaEventoENovoRegistroComoCriado()
    {
        var db = CriarDb(nameof(ProcessarAsync_Criar_SemRegistroExistente_CriaEventoENovoRegistroComoCriado));
        var calendario = new CalendarioTeamsServiceFalso { GraphEventIdARetornar = "graph-evt-1" };
        var origemId = Guid.NewGuid();
        var organizadorId = Guid.NewGuid();

        await CalendarioTeamsMensagemHandler.ProcessarAsync(
            NovaMensagem(OperacaoCalendarioTeams.Criar, origemId, organizadorId), db, calendario, CancellationToken.None);

        Assert.Single(calendario.EventosCriados);
        var registro = await db.CalendariosEventosTeams.SingleAsync(c => c.EntidadeOrigemId == origemId);
        Assert.Equal(StatusCalendarioEvento.Criado, registro.Status);
        Assert.Equal("graph-evt-1", registro.GraphEventId);
        Assert.Null(registro.MensagemErro);
    }

    [Fact]
    public async Task ProcessarAsync_Criar_ComRegistroJaCriado_TrataComoAtualizacaoParaNaoDuplicar()
    {
        var db = CriarDb(nameof(ProcessarAsync_Criar_ComRegistroJaCriado_TrataComoAtualizacaoParaNaoDuplicar));
        var origemId = Guid.NewGuid();
        var organizadorId = Guid.NewGuid();
        db.CalendariosEventosTeams.Add(new CalendarioEventoTeams
        {
            EntidadeOrigemTipo = "Alerta",
            EntidadeOrigemId = origemId,
            OrganizadorUsuarioId = organizadorId,
            Status = StatusCalendarioEvento.Criado,
            GraphEventId = "graph-evt-existente",
        });
        await db.SaveChangesAsync();
        var calendario = new CalendarioTeamsServiceFalso();

        await CalendarioTeamsMensagemHandler.ProcessarAsync(
            NovaMensagem(OperacaoCalendarioTeams.Criar, origemId, organizadorId), db, calendario, CancellationToken.None);

        Assert.Empty(calendario.EventosCriados);
        Assert.Single(calendario.EventosAtualizados);
        Assert.Equal("graph-evt-existente", calendario.EventosAtualizados[0].GraphEventId);
        var registro = await db.CalendariosEventosTeams.SingleAsync(c => c.EntidadeOrigemId == origemId);
        Assert.Equal(StatusCalendarioEvento.Criado, registro.Status);
    }

    [Fact]
    public async Task ProcessarAsync_Atualizar_ComRegistroCriado_ChamaAtualizarEventoAsync()
    {
        var db = CriarDb(nameof(ProcessarAsync_Atualizar_ComRegistroCriado_ChamaAtualizarEventoAsync));
        var origemId = Guid.NewGuid();
        var organizadorId = Guid.NewGuid();
        db.CalendariosEventosTeams.Add(new CalendarioEventoTeams
        {
            EntidadeOrigemTipo = "Alerta",
            EntidadeOrigemId = origemId,
            OrganizadorUsuarioId = organizadorId,
            Status = StatusCalendarioEvento.Criado,
            GraphEventId = "graph-evt-1",
        });
        await db.SaveChangesAsync();
        var calendario = new CalendarioTeamsServiceFalso();

        await CalendarioTeamsMensagemHandler.ProcessarAsync(
            NovaMensagem(OperacaoCalendarioTeams.Atualizar, origemId, organizadorId, titulo: "Novo título"),
            db, calendario, CancellationToken.None);

        Assert.Single(calendario.EventosAtualizados);
        Assert.Equal("Novo título", calendario.EventosAtualizados[0].Titulo);
    }

    [Fact]
    public async Task ProcessarAsync_Atualizar_SemRegistroExistente_Descarta()
    {
        var db = CriarDb(nameof(ProcessarAsync_Atualizar_SemRegistroExistente_Descarta));
        var calendario = new CalendarioTeamsServiceFalso();

        await CalendarioTeamsMensagemHandler.ProcessarAsync(
            NovaMensagem(OperacaoCalendarioTeams.Atualizar, Guid.NewGuid(), Guid.NewGuid()), db, calendario, CancellationToken.None);

        Assert.Empty(calendario.EventosAtualizados);
        Assert.Empty(await db.CalendariosEventosTeams.ToListAsync());
    }

    [Fact]
    public async Task ProcessarAsync_Atualizar_ComRegistroNuncaCriadoComSucesso_Descarta()
    {
        var db = CriarDb(nameof(ProcessarAsync_Atualizar_ComRegistroNuncaCriadoComSucesso_Descarta));
        var origemId = Guid.NewGuid();
        var organizadorId = Guid.NewGuid();
        db.CalendariosEventosTeams.Add(new CalendarioEventoTeams
        {
            EntidadeOrigemTipo = "Alerta",
            EntidadeOrigemId = origemId,
            OrganizadorUsuarioId = organizadorId,
            Status = StatusCalendarioEvento.Falhou,
            MensagemErro = "erro anterior",
        });
        await db.SaveChangesAsync();
        var calendario = new CalendarioTeamsServiceFalso();

        await CalendarioTeamsMensagemHandler.ProcessarAsync(
            NovaMensagem(OperacaoCalendarioTeams.Atualizar, origemId, organizadorId), db, calendario, CancellationToken.None);

        Assert.Empty(calendario.EventosAtualizados);
    }

    [Fact]
    public async Task ProcessarAsync_Cancelar_ComRegistroCriado_ChamaCancelarECancelaRegistro()
    {
        var db = CriarDb(nameof(ProcessarAsync_Cancelar_ComRegistroCriado_ChamaCancelarECancelaRegistro));
        var origemId = Guid.NewGuid();
        var organizadorId = Guid.NewGuid();
        db.CalendariosEventosTeams.Add(new CalendarioEventoTeams
        {
            EntidadeOrigemTipo = "Alerta",
            EntidadeOrigemId = origemId,
            OrganizadorUsuarioId = organizadorId,
            Status = StatusCalendarioEvento.Criado,
            GraphEventId = "graph-evt-1",
        });
        await db.SaveChangesAsync();
        var calendario = new CalendarioTeamsServiceFalso();

        await CalendarioTeamsMensagemHandler.ProcessarAsync(
            NovaMensagem(OperacaoCalendarioTeams.Cancelar, origemId, organizadorId, titulo: null, descricao: null, data: null),
            db, calendario, CancellationToken.None);

        Assert.Single(calendario.EventosCancelados);
        var registro = await db.CalendariosEventosTeams.SingleAsync(c => c.EntidadeOrigemId == origemId);
        Assert.Equal(StatusCalendarioEvento.Cancelado, registro.Status);
    }

    [Fact]
    public async Task ProcessarAsync_Cancelar_SemRegistroExistente_Descarta()
    {
        var db = CriarDb(nameof(ProcessarAsync_Cancelar_SemRegistroExistente_Descarta));
        var calendario = new CalendarioTeamsServiceFalso();

        await CalendarioTeamsMensagemHandler.ProcessarAsync(
            NovaMensagem(OperacaoCalendarioTeams.Cancelar, Guid.NewGuid(), Guid.NewGuid(), titulo: null, descricao: null, data: null),
            db, calendario, CancellationToken.None);

        Assert.Empty(calendario.EventosCancelados);
    }

    [Fact]
    public async Task ProcessarAsync_QuandoCalendarioLancaExcecao_MarcaRegistroComoFalhouERelancaExcecao()
    {
        var db = CriarDb(nameof(ProcessarAsync_QuandoCalendarioLancaExcecao_MarcaRegistroComoFalhouERelancaExcecao));
        var origemId = Guid.NewGuid();
        var organizadorId = Guid.NewGuid();
        var calendario = new CalendarioTeamsServiceFalso { ExcecaoAoCriar = new InvalidOperationException("Graph indisponível") };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CalendarioTeamsMensagemHandler.ProcessarAsync(
            NovaMensagem(OperacaoCalendarioTeams.Criar, origemId, organizadorId), db, calendario, CancellationToken.None));

        Assert.Equal("Graph indisponível", ex.Message);
        var registro = await db.CalendariosEventosTeams.SingleAsync(c => c.EntidadeOrigemId == origemId);
        Assert.Equal(StatusCalendarioEvento.Falhou, registro.Status);
        Assert.Equal("Graph indisponível", registro.MensagemErro);
    }
}

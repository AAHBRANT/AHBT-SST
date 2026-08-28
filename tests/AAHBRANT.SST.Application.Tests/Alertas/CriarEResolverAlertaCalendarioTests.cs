using AAHBRANT.SST.Application.Alertas.Commands;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Integracao.Bot;
using AAHBRANT.SST.Infrastructure.Persistencia;`nusing AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Alertas;

// Cobre o ciclo completo do canal de calendário do Teams a partir dos Commands manuais (docs/
// superpowers/specs/2026-08-28-calendario-teams-design.md §4.4): criar um Alerta com destinatário e
// data limite enfileira "Criar"; resolvê-lo depois — já com o evento de fato criado no Graph —
// enfileira "Cancelar". A ordem importa: Cancelar só é enfileirado porque a mensagem "Criar" já foi
// consumida (Status=Criado em CalendarioEventoTeams) antes do Resolver rodar.
public class CriarEResolverAlertaCalendarioTests
{
    private static IAppDbContext CriarDb(string nomeBanco) =>
        new SstDbContext(new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nomeBanco).Options, new CurrentUserService());

    [Fact]
    public async Task CicloCompleto_CriarDepoisResolver_EnfileiraCriarENoFinalCancelarNestaOrdem()
    {
        var db = CriarDb(nameof(CicloCompleto_CriarDepoisResolver_EnfileiraCriarENoFinalCancelarNestaOrdem));
        var responsavelId = Guid.NewGuid();
        db.Usuarios.Add(new Usuario { Id = responsavelId, Email = "responsavel@aahbrant.com", Nome = "Responsável" });
        await db.SaveChangesAsync();

        var filaNotificacao = new FilaNotificacaoTeamsFalsa();
        var filaCalendario = new FilaCalendarioTeamsFalsa();
        var calendario = new CalendarioTeamsServiceFalso { GraphEventIdARetornar = "graph-evt-ciclo" };

        var criarHandler = new CriarAlertaCommandHandler(db, filaNotificacao, filaCalendario);
        var alertaId = await criarHandler.Handle(
            new CriarAlertaCommand(
                TipoAlerta.AsoVencendo, SeveridadeAlerta.Atencao, "ASO vencendo", null,
                "Aso", Guid.NewGuid(), null, null, responsavelId, DateTime.UtcNow.Date.AddDays(5)),
            CancellationToken.None);

        Assert.Single(filaCalendario.Mensagens);
        Assert.Equal(OperacaoCalendarioTeams.Criar, filaCalendario.Mensagens[0].Operacao);

        // Simula o consumidor de background processando a mensagem "Criar" antes do alerta ser
        // resolvido — sem isso, ResolverAlertaCommand não encontraria Status=Criado e não
        // enfileiraria o "Cancelar" (ver ResolverAlertaCommandHandler.existeEventoCriado).
        await CalendarioTeamsMensagemHandler.ProcessarAsync(filaCalendario.Mensagens[0], db, calendario, CancellationToken.None);

        var resolverHandler = new ResolverAlertaCommandHandler(db, filaCalendario);
        await resolverHandler.Handle(new ResolverAlertaCommand(alertaId), CancellationToken.None);

        Assert.Equal(2, filaCalendario.Mensagens.Count);
        Assert.Equal(OperacaoCalendarioTeams.Criar, filaCalendario.Mensagens[0].Operacao);
        Assert.Equal(OperacaoCalendarioTeams.Cancelar, filaCalendario.Mensagens[1].Operacao);
        Assert.Equal(alertaId, filaCalendario.Mensagens[1].EntidadeOrigemId);
    }

    [Fact]
    public async Task CicloCompleto_ResolverAntesDoCriarSerConsumido_NaoEnfileiraCancelar()
    {
        var db = CriarDb(nameof(CicloCompleto_ResolverAntesDoCriarSerConsumido_NaoEnfileiraCancelar));
        var responsavelId = Guid.NewGuid();
        db.Usuarios.Add(new Usuario { Id = responsavelId, Email = "responsavel@aahbrant.com", Nome = "Responsável" });
        await db.SaveChangesAsync();

        var filaNotificacao = new FilaNotificacaoTeamsFalsa();
        var filaCalendario = new FilaCalendarioTeamsFalsa();

        var criarHandler = new CriarAlertaCommandHandler(db, filaNotificacao, filaCalendario);
        var alertaId = await criarHandler.Handle(
            new CriarAlertaCommand(
                TipoAlerta.AsoVencendo, SeveridadeAlerta.Atencao, "ASO vencendo", null,
                "Aso", Guid.NewGuid(), null, null, responsavelId, DateTime.UtcNow.Date.AddDays(5)),
            CancellationToken.None);

        // Resolve imediatamente, sem o consumidor de background ter processado o "Criar" ainda —
        // não há CalendarioEventoTeams com Status=Criado, então não há o que cancelar no Graph.
        var resolverHandler = new ResolverAlertaCommandHandler(db, filaCalendario);
        await resolverHandler.Handle(new ResolverAlertaCommand(alertaId), CancellationToken.None);

        Assert.Single(filaCalendario.Mensagens);
        Assert.Equal(OperacaoCalendarioTeams.Criar, filaCalendario.Mensagens[0].Operacao);
    }
}

using AAHBRANT.SST.Application.Alertas.Motor;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Domain.Interfaces;
using AAHBRANT.SST.Infrastructure.Integracao.Bot;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Alertas;

// Cobre o cenário de idempotência descrito em docs/superpowers/specs/2026-08-28-calendario-teams-
// design.md §4.4/§8: rodar o motor duas vezes para o mesmo item vencendo não pode duplicar o
// CalendarioEventoTeams nem criar um segundo evento no Graph — a 2ª execução tem que enfileirar
// "Atualizar", não "Criar" (ver AlertaEngineService linhas 135-143: todo run com alerta existente e
// destinatário reenfileira "Atualizar", incondicionalmente; a deduplicação de fato do evento do Graph
// acontece aqui, no CalendarioTeamsMensagemHandler, ao consumir essas mensagens em sequência).
public class AlertaEngineServiceCalendarioIdempotenciaTests
{
    private static IAppDbContext CriarDb(string nomeBanco) =>
        new SstDbContext(new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nomeBanco).Options, new CurrentUserService());

    [Fact]
    public async Task ProcessarAsync_ChamadoDuasVezesParaMesmoItem_NaoDuplicaEventoDeCalendario()
    {
        var db = CriarDb(nameof(ProcessarAsync_ChamadoDuasVezesParaMesmoItem_NaoDuplicaEventoDeCalendario));
        var responsavelId = Guid.NewGuid();
        db.Usuarios.Add(new Usuario { Id = responsavelId, Email = "responsavel@aahbrant.com", Nome = "Responsável" });
        db.RegrasAlerta.Add(new RegraAlerta
        {
            Modulo = TipoModuloAlerta.Aso,
            DiasAntecedencia = 30,
            Severidade = SeveridadeAlerta.Atencao,
            ResponsavelUsuarioId = responsavelId,
        });
        await db.SaveChangesAsync();

        var origemId = Guid.NewGuid();
        var provider = new AlertaOrigemProviderFalso
        {
            Modulo = TipoModuloAlerta.Aso,
            Itens = new List<AlertaOrigemItem>
            {
                new()
                {
                    EntidadeOrigemTipo = "Aso",
                    EntidadeOrigemId = origemId,
                    DataVencimento = DateTime.UtcNow.Date.AddDays(10),
                    TipoAlertaVencendo = TipoAlerta.AsoVencendo,
                    TipoAlertaVencido = TipoAlerta.AsoVencido,
                    Titulo = "ASO João",
                },
            },
        };
        var filaNotificacao = new FilaNotificacaoTeamsFalsa();
        var filaCalendario = new FilaCalendarioTeamsFalsa();
        var calendario = new CalendarioTeamsServiceFalso { GraphEventIdARetornar = "graph-evt-idempotencia" };
        var engine = new AlertaEngineService(db, new List<IAlertaOrigemProvider> { provider }, filaNotificacao, filaCalendario);

        // 1ª execução: cria o Alerta e enfileira "Criar".
        await engine.ProcessarAsync();

        Assert.Single(filaCalendario.Mensagens);
        Assert.Equal(OperacaoCalendarioTeams.Criar, filaCalendario.Mensagens[0].Operacao);

        // Simula o consumidor de background processando a mensagem antes da 2ª execução do motor.
        await CalendarioTeamsMensagemHandler.ProcessarAsync(filaCalendario.Mensagens[0], db, calendario, CancellationToken.None);
        Assert.Single(calendario.EventosCriados);

        // 2ª execução: mesmo item, mesmo Alerta já existente — motor sempre reenfileira "Atualizar"
        // quando há destinatário (comportamento documentado, não é bug).
        await engine.ProcessarAsync();

        Assert.Equal(2, filaCalendario.Mensagens.Count);
        Assert.Equal(OperacaoCalendarioTeams.Atualizar, filaCalendario.Mensagens[1].Operacao);

        await CalendarioTeamsMensagemHandler.ProcessarAsync(filaCalendario.Mensagens[1], db, calendario, CancellationToken.None);

        // O ponto central do teste: mesmo após 2 execuções do motor + 2 mensagens consumidas, só
        // existe 1 evento criado no Graph (nunca um 2º CriarEventoAsync) e 1 única linha em
        // CalendarioEventoTeams para essa origem.
        Assert.Single(calendario.EventosCriados);
        Assert.Single(calendario.EventosAtualizados);
        var alertaId = (await db.Alertas.SingleAsync()).Id;
        var registros = await db.CalendariosEventosTeams.Where(c => c.EntidadeOrigemId == alertaId).ToListAsync();
        Assert.Single(registros);
        Assert.Equal(StatusCalendarioEvento.Criado, registros[0].Status);
        Assert.Equal("graph-evt-idempotencia", registros[0].GraphEventId);
    }
}

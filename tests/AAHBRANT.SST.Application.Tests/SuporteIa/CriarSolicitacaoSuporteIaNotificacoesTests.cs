using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.SuporteIa;
using AAHBRANT.SST.Application.SuporteIa.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.SuporteIa;

// Pedido do usuário (24/09/2026): todo chamado do Suporte IA avisa no sininho do Teams e vira um
// evento no calendário do responsável, na data do prazo pela severidade — antes só a demanda
// técnica chegava no Teams. O Telegram continua recebendo.
public class CriarSolicitacaoSuporteIaNotificacoesTests
{
    private sealed class TriagemFixa(bool requerAlteracao) : ISuporteIaTriagemService
    {
        public Task<TriagemSuporteIaResultado> TriarAsync(SuporteIaEntradaTriagem entrada, CancellationToken ct) =>
            Task.FromResult(new TriagemSuporteIaResultado(
                requerAlteracao ? ResultadoTriagemSuporteIa.DemandaTecnica : ResultadoTriagemSuporteIa.RespostaAoUsuario,
                requerAlteracao, "Resposta da IA", "Demanda reduzida", "Solução", null));
    }

    private sealed class TelegramFalso : ITelegramSuporteService
    {
        public int Envios;
        public Task EnviarDemandaAsync(string mensagem, CancellationToken ct = default) { Envios++; return Task.CompletedTask; }
    }

    private sealed class FilaFalsa : IFilaNotificacaoTeams
    {
        public List<NotificacaoTeamsMensagem> Mensagens { get; } = new();
        public Task EnfileirarAsync(NotificacaoTeamsMensagem mensagem, CancellationToken ct = default)
        {
            Mensagens.Add(mensagem);
            return Task.CompletedTask;
        }
    }

    private sealed class FilaCalendarioFalsa : IFilaCalendarioTeams
    {
        public List<CalendarioTeamsMensagem> Mensagens { get; } = new();
        public Task EnfileirarAsync(CalendarioTeamsMensagem mensagem, CancellationToken ct = default)
        {
            Mensagens.Add(mensagem);
            return Task.CompletedTask;
        }
    }

    private sealed class ConfiguracaoFixa(string? email) : ISuporteIaConfiguracao
    {
        public Guid? ResponsavelUsuarioId => null;
        public string? ResponsavelEmail => email;
    }

    private static CriarSolicitacaoSuporteIaCommand Comando(SeveridadeSolicitacaoSuporteIa severidade = SeveridadeSolicitacaoSuporteIa.Media) => new(
        TipoSolicitacaoSuporteIa.Duvida, severidade,
        "Como encerro a inspeção?", "Não acho o botão", "Inspeções", null, null, "Fulano", "fulano@aahbrant.com");

    private static async Task<(IAppDbContext db, Usuario responsavel)> CriarDbAsync(string nome)
    {
        var db = new SstDbContext(new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nome).Options, new CurrentUserService());
        var responsavel = new Usuario { Nome = "Wellington", Email = "wellington@aahbrant.com" };
        db.Usuarios.Add(responsavel);
        await db.SaveChangesAsync();
        return (db, responsavel);
    }

    [Fact]
    public async Task Duvida_RespondidaPelaIa_AvisaSininhoECriaEventoComAlertaJaResolvido()
    {
        var (db, responsavel) = await CriarDbAsync(nameof(Duvida_RespondidaPelaIa_AvisaSininhoECriaEventoComAlertaJaResolvido));
        var telegram = new TelegramFalso();
        var fila = new FilaFalsa();
        var calendario = new FilaCalendarioFalsa();

        await new CriarSolicitacaoSuporteIaCommandHandler(db, new TriagemFixa(false), telegram, fila, calendario,
            new ConfiguracaoFixa(responsavel.Email)).Handle(Comando(), default);

        Assert.Equal(1, telegram.Envios);
        var mensagem = Assert.Single(fila.Mensagens);
        Assert.Equal(responsavel.Id, mensagem.DestinatarioUsuarioId);
        Assert.Equal("Resposta da IA", mensagem.Descricao);
        var alerta = await db.Alertas.SingleAsync();
        Assert.Equal(StatusAlerta.Resolvido, alerta.Status);
        Assert.Equal(SeveridadeAlerta.Info, alerta.Severidade);

        var evento = Assert.Single(calendario.Mensagens);
        Assert.Equal(OperacaoCalendarioTeams.Criar, evento.Operacao);
        Assert.Equal("Alerta", evento.EntidadeOrigemTipo);
        Assert.Equal(alerta.Id, evento.EntidadeOrigemId);
        Assert.Equal(responsavel.Id, evento.OrganizadorUsuarioId);
        Assert.NotNull(evento.Data);
    }

    [Fact]
    public async Task DemandaTecnica_AvisaSininhoComAlertaAberto()
    {
        var (db, responsavel) = await CriarDbAsync(nameof(DemandaTecnica_AvisaSininhoComAlertaAberto));
        var fila = new FilaFalsa();

        await new CriarSolicitacaoSuporteIaCommandHandler(db, new TriagemFixa(true), new TelegramFalso(), fila, new FilaCalendarioFalsa(),
            new ConfiguracaoFixa(responsavel.Email)).Handle(Comando(), default);

        Assert.Equal("Demanda reduzida", Assert.Single(fila.Mensagens).Descricao);
        Assert.Equal(StatusAlerta.Aberto, (await db.Alertas.SingleAsync()).Status);
    }

    [Fact]
    public async Task SemResponsavelConfigurado_SoTelegramRecebe()
    {
        var (db, _) = await CriarDbAsync(nameof(SemResponsavelConfigurado_SoTelegramRecebe));
        var telegram = new TelegramFalso();
        var fila = new FilaFalsa();
        var calendario = new FilaCalendarioFalsa();

        await new CriarSolicitacaoSuporteIaCommandHandler(db, new TriagemFixa(false), telegram, fila, calendario,
            new ConfiguracaoFixa("outra.pessoa@aahbrant.com")).Handle(Comando(), default);

        Assert.Equal(1, telegram.Envios);
        Assert.Empty(fila.Mensagens);
        Assert.Empty(calendario.Mensagens);
        Assert.Empty(await db.Alertas.ToListAsync());
    }

    // Sexta-feira, 25/09/2026, 10h em Brasília (13h UTC).
    [Theory]
    [InlineData(SeveridadeSolicitacaoSuporteIa.Critica, "2026-09-25")]
    [InlineData(SeveridadeSolicitacaoSuporteIa.Alta, "2026-09-28")]
    [InlineData(SeveridadeSolicitacaoSuporteIa.Media, "2026-09-30")]
    [InlineData(SeveridadeSolicitacaoSuporteIa.Baixa, "2026-10-02")]
    public void CalcularPrazo_ContaDiasUteisPulandoFimDeSemana(SeveridadeSolicitacaoSuporteIa severidade, string esperado)
    {
        var prazo = SuporteIaCalendario.CalcularPrazo(severidade, new DateTime(2026, 9, 25, 13, 0, 0, DateTimeKind.Utc));

        Assert.Equal(DateTime.Parse(esperado), prazo);
    }

    [Fact]
    public void CalcularPrazo_UsaODiaDeBrasiliaNaoODiaUtc()
    {
        // 23h de quinta em Brasília já é sexta em UTC — o prazo crítico é quinta.
        var prazo = SuporteIaCalendario.CalcularPrazo(
            SeveridadeSolicitacaoSuporteIa.Critica, new DateTime(2026, 9, 25, 2, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 9, 24), prazo);
    }

    [Fact]
    public async Task Recusar_ChamadoComEventoCriado_ResolveAlertaECancelaEvento()
    {
        var (db, responsavel) = await CriarDbAsync(nameof(Recusar_ChamadoComEventoCriado_ResolveAlertaECancelaEvento));
        await new CriarSolicitacaoSuporteIaCommandHandler(db, new TriagemFixa(true), new TelegramFalso(), new FilaFalsa(),
            new FilaCalendarioFalsa(), new ConfiguracaoFixa(responsavel.Email)).Handle(Comando(), default);
        var solicitacao = await db.SuporteIaSolicitacoes.SingleAsync();
        db.CalendariosEventosTeams.Add(new CalendarioEventoTeams
        {
            EntidadeOrigemTipo = "Alerta",
            EntidadeOrigemId = solicitacao.AlertaId!.Value,
            OrganizadorUsuarioId = responsavel.Id,
            Status = StatusCalendarioEvento.Criado,
        });
        await db.SaveChangesAsync();
        var calendario = new FilaCalendarioFalsa();

        await new RecusarSolicitacaoSuporteIaCommandHandler(db, calendario)
            .Handle(new RecusarSolicitacaoSuporteIaCommand(solicitacao.Id, "Fora do escopo"), default);

        Assert.Equal(StatusAlerta.Resolvido, (await db.Alertas.SingleAsync()).Status);
        Assert.Equal(OperacaoCalendarioTeams.Cancelar, Assert.Single(calendario.Mensagens).Operacao);
    }
}

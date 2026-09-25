using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.SuporteIa;
using AAHBRANT.SST.Application.SuporteIa.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.SuporteIa;

// Pedido do usuário (24/09/2026): todo chamado do Suporte IA avisa no sininho do Teams e no chat do
// Teams (via Workflow), além do Telegram — antes só a demanda técnica chegava no Teams.
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

    private sealed class WorkflowFalso : ITeamsWorkflowSuporteService
    {
        public List<SuporteIaSolicitacao> Enviadas { get; } = new();
        public Task EnviarDemandaAsync(SuporteIaSolicitacao solicitacao, CancellationToken ct = default)
        {
            Enviadas.Add(solicitacao);
            return Task.CompletedTask;
        }
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

    private sealed class ConfiguracaoFixa(string? email) : ISuporteIaConfiguracao
    {
        public Guid? ResponsavelUsuarioId => null;
        public string? ResponsavelEmail => email;
    }

    private static CriarSolicitacaoSuporteIaCommand Comando() => new(
        TipoSolicitacaoSuporteIa.Duvida, SeveridadeSolicitacaoSuporteIa.Media,
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
    public async Task Duvida_RespondidaPelaIa_AvisaSininhoEChatComAlertaJaResolvido()
    {
        var (db, responsavel) = await CriarDbAsync(nameof(Duvida_RespondidaPelaIa_AvisaSininhoEChatComAlertaJaResolvido));
        var telegram = new TelegramFalso();
        var workflow = new WorkflowFalso();
        var fila = new FilaFalsa();

        await new CriarSolicitacaoSuporteIaCommandHandler(db, new TriagemFixa(false), telegram, workflow, fila,
            new ConfiguracaoFixa(responsavel.Email)).Handle(Comando(), default);

        Assert.Equal(1, telegram.Envios);
        Assert.Single(workflow.Enviadas);
        var mensagem = Assert.Single(fila.Mensagens);
        Assert.Equal(responsavel.Id, mensagem.DestinatarioUsuarioId);
        Assert.Equal("Resposta da IA", mensagem.Descricao);
        var alerta = await db.Alertas.SingleAsync();
        Assert.Equal(StatusAlerta.Resolvido, alerta.Status);
        Assert.Equal(SeveridadeAlerta.Info, alerta.Severidade);
    }

    [Fact]
    public async Task DemandaTecnica_AvisaSininhoComAlertaAberto()
    {
        var (db, responsavel) = await CriarDbAsync(nameof(DemandaTecnica_AvisaSininhoComAlertaAberto));
        var fila = new FilaFalsa();

        await new CriarSolicitacaoSuporteIaCommandHandler(db, new TriagemFixa(true), new TelegramFalso(), new WorkflowFalso(), fila,
            new ConfiguracaoFixa(responsavel.Email)).Handle(Comando(), default);

        Assert.Equal("Demanda reduzida", Assert.Single(fila.Mensagens).Descricao);
        Assert.Equal(StatusAlerta.Aberto, (await db.Alertas.SingleAsync()).Status);
    }

    [Fact]
    public async Task SemResponsavelConfigurado_ChatETelegramContinuamRecebendo()
    {
        var (db, _) = await CriarDbAsync(nameof(SemResponsavelConfigurado_ChatETelegramContinuamRecebendo));
        var telegram = new TelegramFalso();
        var workflow = new WorkflowFalso();
        var fila = new FilaFalsa();

        await new CriarSolicitacaoSuporteIaCommandHandler(db, new TriagemFixa(false), telegram, workflow, fila,
            new ConfiguracaoFixa("outra.pessoa@aahbrant.com")).Handle(Comando(), default);

        Assert.Equal(1, telegram.Envios);
        Assert.Single(workflow.Enviadas);
        Assert.Empty(fila.Mensagens);
        Assert.Empty(await db.Alertas.ToListAsync());
    }
}

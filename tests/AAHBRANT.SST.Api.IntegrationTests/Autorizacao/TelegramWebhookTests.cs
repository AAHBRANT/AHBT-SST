using System.Text.Json;
using AAHBRANT.SST.Api.Controllers;
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Infrastructure.Seguranca;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AAHBRANT.SST.Api.IntegrationTests.Autorizacao;

// Webhook dos botões Aprovar/Recusar: só o Telegram (segredo) e só o responsável (usuário do chat de alertas)
// conseguem decidir uma proposta do agente engenheiro da assinatura.
public class TelegramWebhookTests
{
    private const string Segredo = "segredo-do-webhook-com-mais-de-32-caracteres";
    private const long Responsavel = 8226485025;

    private sealed class TelegramFalso : ITelegramAlertaService
    {
        public List<string> Respostas { get; } = new();
        public Task<bool> EnviarAsync(string mensagem, CancellationToken ct = default) => Task.FromResult(true);
        public Task<long?> EnviarComBotoesAsync(string mensagem, IReadOnlyList<BotaoTelegram> botoes, CancellationToken ct = default) => Task.FromResult<long?>(1);
        public Task EditarAsync(long mensagemId, string mensagem, CancellationToken ct = default) => Task.CompletedTask;
        public Task ResponderCliqueAsync(string cliqueId, string aviso, CancellationToken ct = default)
        {
            Respostas.Add(aviso);
            return Task.CompletedTask;
        }
        public bool UsuarioAutorizado(long usuarioTelegramId) => usuarioTelegramId == Responsavel;
        public Task<bool> RegistrarWebhookAsync(string url, string segredo, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class MediatorFalso : IMediator
    {
        public List<object> Pedidos { get; } = new();
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            Pedidos.Add(request);
            return Task.FromResult((TResponse)(object)"Feito.");
        }
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => throw new NotSupportedException();
    }

    private static (TelegramWebhookController Controller, MediatorFalso Mediator, TelegramFalso Telegram, CurrentUserService Usuario) Montar(string? segredoConfigurado, string? segredoEnviado)
    {
        var mediator = new MediatorFalso();
        var telegram = new TelegramFalso();
        var usuario = new CurrentUserService();
        usuario.DefinirEscopo(temAcessoGlobal: false, Array.Empty<Guid>());
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [TelegramWebhookController.ConfiguracaoSegredo] = segredoConfigurado })
            .Build();
        var controller = new TelegramWebhookController(mediator, telegram, usuario, configuracao, NullLogger<TelegramWebhookController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        if (segredoEnviado is not null)
            controller.HttpContext.Request.Headers[TelegramWebhookController.Cabecalho] = segredoEnviado;
        return (controller, mediator, telegram, usuario);
    }

    private static JsonElement Clique(long de, string dado) => JsonDocument.Parse(JsonSerializer.Serialize(new
    {
        update_id = 1,
        callback_query = new { id = "clique-1", from = new { id = de }, data = dado },
    })).RootElement;

    private static readonly Guid Proposta = Guid.NewGuid();

    [Fact]
    public async Task SemSegredoConfigurado_RotaDesligada()
    {
        var (controller, mediator, _, _) = Montar(null, null);

        Assert.IsType<NotFoundResult>(await controller.Receber(Clique(Responsavel, PropostaAcaoAssinaturaRegras.DadoBotao(Proposta, true)), CancellationToken.None));
        Assert.Empty(mediator.Pedidos);
    }

    [Fact]
    public async Task SegredoErrado_401_SemDecidir()
    {
        var (controller, mediator, _, _) = Montar(Segredo, Segredo + "x");

        Assert.IsType<UnauthorizedResult>(await controller.Receber(Clique(Responsavel, PropostaAcaoAssinaturaRegras.DadoBotao(Proposta, true)), CancellationToken.None));
        Assert.Empty(mediator.Pedidos);
    }

    [Fact]
    public async Task CliqueDeOutraConta_NaoDecide()
    {
        var (controller, mediator, telegram, usuario) = Montar(Segredo, Segredo);

        Assert.IsType<OkResult>(await controller.Receber(Clique(123456, PropostaAcaoAssinaturaRegras.DadoBotao(Proposta, true)), CancellationToken.None));
        Assert.Empty(mediator.Pedidos);
        Assert.Contains("permissão", Assert.Single(telegram.Respostas));
        Assert.False(usuario.TemAcessoGlobal);
    }

    [Fact]
    public async Task BotaoInvalido_NaoDecide()
    {
        var (controller, mediator, telegram, _) = Montar(Segredo, Segredo);

        await controller.Receber(Clique(Responsavel, "qualquer-coisa"), CancellationToken.None);

        Assert.Empty(mediator.Pedidos);
        Assert.Single(telegram.Respostas);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CliqueDoResponsavel_Decide_EResponde(bool aprovar)
    {
        var (controller, mediator, telegram, usuario) = Montar(Segredo, Segredo);

        Assert.IsType<OkResult>(await controller.Receber(Clique(Responsavel, PropostaAcaoAssinaturaRegras.DadoBotao(Proposta, aprovar)), CancellationToken.None));

        var decisao = Assert.IsType<DecidirPropostaAcaoAssinaturaCommand>(Assert.Single(mediator.Pedidos));
        Assert.Equal(Proposta, decisao.PropostaId);
        Assert.Equal(aprovar, decisao.Aprovar);
        Assert.Equal("Feito.", Assert.Single(telegram.Respostas));
        Assert.True(usuario.TemAcessoGlobal);
    }

    [Fact]
    public async Task AtualizacaoSemClique_IgnoradaCom200()
    {
        var (controller, mediator, _, _) = Montar(Segredo, Segredo);
        var mensagemComum = JsonDocument.Parse("""{"update_id":2,"message":{"text":"oi"}}""").RootElement;

        Assert.IsType<OkResult>(await controller.Receber(mensagemComum, CancellationToken.None));
        Assert.Empty(mediator.Pedidos);
    }
}

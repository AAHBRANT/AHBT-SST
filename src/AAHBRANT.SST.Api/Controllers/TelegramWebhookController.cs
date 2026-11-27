using System.Text.Json;
using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AAHBRANT.SST.Api.Controllers;

// Cliques nos botões Aprovar/Recusar das propostas do agente engenheiro da assinatura (27/11). O Telegram
// chama esta rota (registrada por POST api/interno/assinatura/telegram/registrar-webhook) mandando o segredo
// Telegram:WebhookSegredo no cabeçalho X-Telegram-Bot-Api-Secret-Token. Três travas:
// 1. segredo do webhook (só o Telegram conhece);
// 2. o clique precisa vir do usuário do chat de alertas (o responsável), nunca de outra conta;
// 3. a proposta é de uso único e vence em 24 h (DecidirPropostaAcaoAssinaturaCommand).
// Responde 200 mesmo quando ignora a atualização: senão o Telegram fica reenviando.
[ApiController]
[AllowAnonymous]
[EnableRateLimiting("publico")]
[Route(Rota)]
public class TelegramWebhookController : ControllerBase
{
    public const string Rota = "api/telegram/webhook";
    public const string Cabecalho = "X-Telegram-Bot-Api-Secret-Token";
    public const string ConfiguracaoSegredo = "Telegram:WebhookSegredo";

    private readonly IMediator _mediator;
    private readonly ITelegramAlertaService _telegram;
    private readonly ICurrentUserService _usuario;
    private readonly IConfiguration _configuracao;
    private readonly ILogger<TelegramWebhookController> _logger;

    public TelegramWebhookController(IMediator mediator, ITelegramAlertaService telegram, ICurrentUserService usuario,
        IConfiguration configuracao, ILogger<TelegramWebhookController> logger)
    {
        _mediator = mediator;
        _telegram = telegram;
        _usuario = usuario;
        _configuracao = configuracao;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Receber([FromBody] JsonElement atualizacao, CancellationToken ct)
    {
        var segredo = _configuracao[ConfiguracaoSegredo];
        if (string.IsNullOrWhiteSpace(segredo) || segredo.Length < ChaveAgenteAssinaturaAttribute.TamanhoMinimo)
            return NotFound();
        if (!ChaveAgenteAssinaturaAttribute.ChavesIguais(segredo, Request.Headers[Cabecalho].ToString()))
            return Unauthorized();

        if (!atualizacao.TryGetProperty("callback_query", out var clique)
            || !clique.TryGetProperty("id", out var cliqueIdJson)
            || !clique.TryGetProperty("from", out var de) || !de.TryGetProperty("id", out var usuarioJson))
            return Ok();

        var cliqueId = cliqueIdJson.GetString() ?? string.Empty;
        if (!_telegram.UsuarioAutorizado(usuarioJson.GetInt64()))
        {
            _logger.LogWarning("Clique em proposta do agente da assinatura vindo de usuário não autorizado ({Usuario}).", usuarioJson.GetInt64());
            await _telegram.ResponderCliqueAsync(cliqueId, "Você não tem permissão para decidir este pedido.", ct);
            return Ok();
        }

        var botao = PropostaAcaoAssinaturaRegras.LerBotao(clique.TryGetProperty("data", out var dado) ? dado.GetString() : null);
        if (botao is null)
        {
            await _telegram.ResponderCliqueAsync(cliqueId, "Botão não reconhecido.", ct);
            return Ok();
        }

        // Quem decide é o responsável do sistema, sem login do Entra: a ação cobre a obra da proposta.
        _usuario.DefinirEscopo(temAcessoGlobal: true, Array.Empty<Guid>());
        var aviso = await _mediator.Send(new DecidirPropostaAcaoAssinaturaCommand(botao.Value.PropostaId, botao.Value.Aprovar), ct);
        await _telegram.ResponderCliqueAsync(cliqueId, aviso, ct);
        return Ok();
    }
}

using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AAHBRANT.SST.Api.Controllers;

// Rotas do agente engenheiro da assinatura (Claude Console, todo dia às 6h30 e quando a vigia do Worker
// chama) — pedido do usuário em 27/11, depois de o facial cair às 22h sem ninguém saber até o DDS.
// O agente só lê o relatório, avisa no Telegram e faz ações SEGURAS e reversíveis (retreinar grupo).
// Ação irreversível (excluir pessoa do Azure, mudar recurso) não existe aqui: depende do responsável.
[ApiController]
[AllowAnonymous]
[ChaveAgenteAssinatura]
[EnableRateLimiting("agente-anonimo")]
[Route("api/interno/assinatura")]
public class AgenteAssinaturaController : ControllerBase
{
    public const string PrefixoMensagem = "[Agente da assinatura] ";

    private readonly IMediator _mediator;

    public AgenteAssinaturaController(IMediator mediator) => _mediator = mediator;

    [HttpGet("saude")]
    public async Task<ActionResult<SaudeAssinaturaDto>> Saude(CancellationToken ct)
        => Ok(await _mediator.Send(new ObterSaudeAssinaturaQuery(), ct));

    public record AvisarRequest(string? Mensagem);

    [HttpPost("avisar")]
    public async Task<IActionResult> Avisar([FromBody] AvisarRequest corpo, [FromServices] ITelegramAlertaService telegram, CancellationToken ct)
    {
        var mensagem = corpo.Mensagem?.Trim();
        if (string.IsNullOrEmpty(mensagem))
            return BadRequest(new { erro = "Informe a mensagem." });
        if (mensagem.Length > 3500)
            return BadRequest(new { erro = "Mensagem longa demais: resuma em até 3500 caracteres." });

        var enviada = await telegram.EnviarAsync(PrefixoMensagem + mensagem, ct);
        return enviada
            ? Ok(new { enviada = true })
            : StatusCode(StatusCodes.Status502BadGateway, new { enviada = false, erro = "O Telegram não está configurado ou recusou a mensagem." });
    }

    // Tipo por nome ("ExcluirPessoasFaciaisSoltas"): a API não converte enum de texto por padrão.
    public record ProporRequest(string? Tipo, Guid? ObraId, string? Motivo);

    // Ação irreversível: o agente só PEDE. O servidor escolhe os alvos e manda os botões Aprovar/Recusar ao
    // responsável no Telegram; nada é executado aqui. Consulte o desfecho em GET propostas/{id}.
    [HttpPost("propostas")]
    public async Task<ActionResult<PropostaAcaoAssinaturaDto>> Propor([FromBody] ProporRequest corpo, CancellationToken ct)
    {
        if (corpo.ObraId is null || !Enum.TryParse<TipoAcaoAssinatura>(corpo.Tipo, ignoreCase: true, out var tipo)
            || !Enum.IsDefined(tipo) || int.TryParse(corpo.Tipo, out _))
            return BadRequest(new { erro = $"Informe obraId e tipo ({string.Join(", ", Enum.GetNames<TipoAcaoAssinatura>())})." });
        return Ok(await _mediator.Send(new CriarPropostaAcaoAssinaturaCommand(tipo, corpo.ObraId.Value, corpo.Motivo), ct));
    }

    [HttpGet("propostas/{propostaId:guid}")]
    public async Task<ActionResult<PropostaAcaoAssinaturaDto>> Proposta(Guid propostaId, CancellationToken ct)
        => await _mediator.Send(new ObterPropostaAcaoAssinaturaQuery(propostaId), ct) is { } proposta ? Ok(proposta) : NotFound();

    // Uma vez por ambiente (ou ao trocar o segredo): faz o Telegram entregar os cliques dos botões em
    // POST api/telegram/webhook, com o segredo Telegram:WebhookSegredo no cabeçalho.
    [HttpPost("telegram/registrar-webhook")]
    public async Task<IActionResult> RegistrarWebhook([FromServices] ITelegramAlertaService telegram, [FromServices] IConfiguration configuracao, CancellationToken ct)
    {
        var segredo = configuracao[TelegramWebhookController.ConfiguracaoSegredo];
        if (string.IsNullOrWhiteSpace(segredo) || segredo.Length < ChaveAgenteAssinaturaAttribute.TamanhoMinimo)
            return BadRequest(new { erro = $"Configure {TelegramWebhookController.ConfiguracaoSegredo} (32 caracteres ou mais)." });
        var url = $"https://{Request.Host}/{TelegramWebhookController.Rota}";
        return await telegram.RegistrarWebhookAsync(url, segredo, ct)
            ? Ok(new { registrado = true, url })
            : StatusCode(StatusCodes.Status502BadGateway, new { registrado = false, erro = "O Telegram recusou o registro (ver log da API)." });
    }

    // Ação segura: recalcula o índice do grupo com as faces que já estão lá. Não cria nem apaga ninguém.
    [HttpPost("obras/{obraId:guid}/retreinar")]
    public async Task<IActionResult> Retreinar(Guid obraId, [FromServices] IAutenticacaoFacialService facial, CancellationToken ct)
    {
        await facial.RetreinarGrupoAsync(obraId, ct);
        return Ok(await facial.ObterTreinoGrupoAsync(obraId, ct));
    }
}

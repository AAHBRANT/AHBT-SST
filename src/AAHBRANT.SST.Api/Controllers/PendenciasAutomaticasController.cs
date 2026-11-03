using System.Security.Claims;
using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Application.PendenciasAutomaticas;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

// Pop-up de pendências automáticas por obra (03/11): recadastro facial e digitais incompletas.
// A lista sai para Técnico de Segurança e Administrador; a configuração é só do Administrador.
[ApiController]
[Route("api/pendencias-automaticas")]
public class PendenciasAutomaticasController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IConfiguration _configuracao;

    public PendenciasAutomaticasController(IMediator mediator, IConfiguration configuracao)
    {
        _mediator = mediator;
        _configuracao = configuracao;
    }

    private bool AutenticacaoHabilitada => !string.IsNullOrWhiteSpace(_configuracao["AzureAd:TenantId"]);
    private string? AzureAdObjectId => User.FindFirst("oid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [Authorize(Policy = "trabalhador:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
        => Ok(await _mediator.Send(new ListarPendenciasAutomaticasQuery(AzureAdObjectId, AutenticacaoHabilitada), ct));

    [Authorize(Policy = PoliticasAutorizacao.SomenteAdministrador)]
    [HttpGet("configuracao")]
    public async Task<IActionResult> ListarConfiguracao(CancellationToken ct)
        => Ok(await _mediator.Send(new ListarConfiguracoesPendenciaQuery(), ct));

    public record SalvarConfiguracaoBody(bool Ligada, FrequenciaPendenciaAutomatica Frequencia, bool PodeFechar);

    [Authorize(Policy = PoliticasAutorizacao.SomenteAdministrador)]
    [HttpPut("configuracao/{tipo}")]
    public async Task<IActionResult> SalvarConfiguracao(TipoPendenciaAutomatica tipo, [FromBody] SalvarConfiguracaoBody corpo, CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new SalvarConfiguracaoPendenciaCommand(tipo, corpo.Ligada, corpo.Frequencia, corpo.PodeFechar), ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }
}

using System.Security.Claims;
using AAHBRANT.SST.Application.PlanosIntegracao.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

// Plano de integração do funcionário novo (especificação 21/10/2026, Módulo 1) — alimenta o pop-up
// "Funcionário novo" que aparece depois das Novidades para o Técnico de Segurança e o Administrador.
[ApiController]
[Route("api/planos-integracao")]
public class PlanosIntegracaoController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IConfiguration _configuracao;

    public PlanosIntegracaoController(IMediator mediator, IConfiguration configuracao)
    {
        _mediator = mediator;
        _configuracao = configuracao;
    }

    private bool AutenticacaoHabilitada => !string.IsNullOrWhiteSpace(_configuracao["AzureAd:TenantId"]);
    private string? AzureAdObjectId => User.FindFirst("oid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [Authorize(Policy = "trabalhador:ver")]
    [HttpGet("pendentes")]
    public async Task<IActionResult> ListarPendentes(CancellationToken ct)
        => Ok(await _mediator.Send(new ListarPlanosIntegracaoPendentesQuery(null, AzureAdObjectId, AutenticacaoHabilitada), ct));

    [Authorize(Policy = "trabalhador:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(Guid id, CancellationToken ct)
    {
        var plano = (await _mediator.Send(new ListarPlanosIntegracaoPendentesQuery(id, AzureAdObjectId, AutenticacaoHabilitada), ct)).FirstOrDefault();
        return plano is null ? NotFound() : Ok(plano);
    }

    [Authorize(Policy = "trabalhador:editar")]
    [HttpPost("{id:guid}/assumir")]
    public async Task<IActionResult> Assumir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new AssumirPlanoIntegracaoCommand(id, AzureAdObjectId), ct);
        return NoContent();
    }
}

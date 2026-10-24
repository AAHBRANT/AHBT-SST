using AAHBRANT.SST.Application.OrdensServico;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

// Ordem de Serviço de SST do trabalhador (20/10/2026). Nesta etapa só a minuta e as pendências; a
// emissão com número e assinatura vem depois.
[ApiController]
[Route("api/trabalhadores/{trabalhadorId:guid}/ordem-servico")]
public class OrdensServicoController : ControllerBase
{
    private readonly IMediator _mediator;

    public OrdensServicoController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "trabalhador:ver")]
    [HttpGet("minuta")]
    public async Task<IActionResult> ObterMinuta(Guid trabalhadorId, CancellationToken ct)
    {
        var minuta = await _mediator.Send(new ObterMinutaOrdemServicoQuery(trabalhadorId), ct);
        return minuta is null ? NotFound() : Ok(minuta);
    }
}

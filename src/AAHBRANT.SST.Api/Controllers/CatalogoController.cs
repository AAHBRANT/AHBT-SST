using AAHBRANT.SST.Application.Catalogo;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

// Catálogo de APR/PT por atividade da obra (PGR). Ver ObterCatalogoAtividadesQuery.
[ApiController]
[Route("api/[controller]")]
public class CatalogoController : ControllerBase
{
    private readonly IMediator _mediator;

    public CatalogoController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "apr:ver")]
    [HttpGet("atividades")]
    public async Task<IActionResult> Atividades([FromQuery] Guid? obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ObterCatalogoAtividadesQuery(obraId), ct));

    [Authorize(Policy = "apr:criar")]
    [HttpPost("apr")]
    public async Task<IActionResult> GerarApr(GerarAprDaAtividadeCommand command, CancellationToken ct)
        => Ok(new { id = await _mediator.Send(command, ct) });

    [Authorize(Policy = "pt:criar")]
    [HttpPost("pt")]
    public async Task<IActionResult> GerarPt(GerarPtDaAtividadeCommand command, CancellationToken ct)
        => Ok(new { id = await _mediator.Send(command, ct) });
}

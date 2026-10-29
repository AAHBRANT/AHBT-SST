using AAHBRANT.SST.Application.Cbo;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

// Busca na tabela oficial da CBO 2002 (29/10/2026), usada no passo Funções e GHE do gerador de PGR.
[ApiController]
[Route("api/cbo")]
public class CboController : ControllerBase
{
    private readonly IMediator _mediator;

    public CboController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "pgr:ver")]
    [HttpGet]
    public async Task<IActionResult> Buscar([FromQuery] string? busca, CancellationToken ct)
        => Ok(await _mediator.Send(new BuscarCboQuery(busca), ct));
}

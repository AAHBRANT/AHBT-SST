using AAHBRANT.SST.Application.Elegibilidade.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ElegibilidadeController : ControllerBase
{
    private readonly IMediator _mediator;

    public ElegibilidadeController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "risco:ver")]
    [HttpPost("avaliar")]
    public async Task<IActionResult> Avaliar(AvaliarElegibilidadeQuery query, CancellationToken ct)
        => Ok(await _mediator.Send(query, ct));
}

using AAHBRANT.SST.Application.PgrRevisoes.Commands;
using AAHBRANT.SST.Application.PgrRevisoes.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PgrRevisoesController : ControllerBase
{
    private readonly IMediator _mediator;

    public PgrRevisoesController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "pgr:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid pgrId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarPgrRevisoesQuery(pgrId), ct));

    [Authorize(Policy = "pgr:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarPgrRevisaoCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(Listar), new { pgrId = command.PgrId }, new { id });
    }
}

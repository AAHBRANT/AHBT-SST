using AAHBRANT.SST.Application.PcmsoRevisoes.Commands;
using AAHBRANT.SST.Application.PcmsoRevisoes.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PcmsoRevisoesController : ControllerBase
{
    private readonly IMediator _mediator;

    public PcmsoRevisoesController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "pcmso:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid pcmsoId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarPcmsoRevisoesQuery(pcmsoId), ct));

    [Authorize(Policy = "pcmso:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarPcmsoRevisaoCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(Listar), new { pcmsoId = command.PcmsoId }, new { id });
    }
}

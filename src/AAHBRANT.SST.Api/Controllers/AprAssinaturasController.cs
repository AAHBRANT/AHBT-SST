using AAHBRANT.SST.Application.AprAssinaturas.Commands;
using AAHBRANT.SST.Application.AprAssinaturas.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AprAssinaturasController : ControllerBase
{
    private readonly IMediator _mediator;

    public AprAssinaturasController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "apr:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid aprId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarAprAssinaturasQuery(aprId), ct));

    [Authorize(Policy = "apr:aprovar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarAprAssinaturaCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(Listar), new { aprId = command.AprId }, new { id });
    }
}

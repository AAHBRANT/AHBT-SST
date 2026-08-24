using AAHBRANT.SST.Application.AprEtapas.Commands;
using AAHBRANT.SST.Application.AprEtapas.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AprEtapasController : ControllerBase
{
    private readonly IMediator _mediator;

    public AprEtapasController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "apr:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid aprId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarAprEtapasQuery(aprId), ct));

    [Authorize(Policy = "apr:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarAprEtapaCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(Listar), new { aprId = command.AprId }, new { id });
    }

    [Authorize(Policy = "apr:editar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirAprEtapaCommand(id), ct);
        return NoContent();
    }
}

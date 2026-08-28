using AAHBRANT.SST.Application.PcmsoItensMatriz.Commands;
using AAHBRANT.SST.Application.PcmsoItensMatriz.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PcmsoItensMatrizController : ControllerBase
{
    private readonly IMediator _mediator;

    public PcmsoItensMatrizController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "pcmso:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid pcmsoId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarItensMatrizQuery(pcmsoId), ct));

    [Authorize(Policy = "pcmso:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarItemMatrizCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(Listar), new { pcmsoId = command.PcmsoId }, new { id });
    }

    [Authorize(Policy = "pcmso:editar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirItemMatrizCommand(id), ct);
        return NoContent();
    }
}

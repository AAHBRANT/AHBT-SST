using AAHBRANT.SST.Application.EntregasEpc.Commands;
using AAHBRANT.SST.Application.EntregasEpc.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EntregasEpcController : ControllerBase
{
    private readonly IMediator _mediator;
    public EntregasEpcController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "epc:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? trabalhadorId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarEntregasEpcQuery(trabalhadorId), ct));

    [Authorize(Policy = "epc:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var entrega = await _mediator.Send(new ObterEntregaEpcPorIdQuery(id), ct);
        return entrega is null ? NotFound() : Ok(entrega);
    }

    [Authorize(Policy = "epc:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarEntregaEpcCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }
}

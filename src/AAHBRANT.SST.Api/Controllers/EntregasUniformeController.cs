using AAHBRANT.SST.Application.EntregasUniforme.Commands;
using AAHBRANT.SST.Application.EntregasUniforme.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EntregasUniformeController : ControllerBase
{
    private readonly IMediator _mediator;
    public EntregasUniformeController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "uniforme:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? trabalhadorId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarEntregasUniformeQuery(trabalhadorId), ct));

    [Authorize(Policy = "uniforme:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var entrega = await _mediator.Send(new ObterEntregaUniformePorIdQuery(id), ct);
        return entrega is null ? NotFound() : Ok(entrega);
    }

    [Authorize(Policy = "uniforme:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarEntregaUniformeCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }
}

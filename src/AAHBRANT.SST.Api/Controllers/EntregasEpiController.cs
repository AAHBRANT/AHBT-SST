using AAHBRANT.SST.Application.EntregasEpi.Commands;
using AAHBRANT.SST.Application.EntregasEpi.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EntregasEpiController : ControllerBase
{
    private readonly IMediator _mediator;

    public EntregasEpiController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "epi:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? trabalhadorId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarEntregasEpiQuery(trabalhadorId), ct));

    [Authorize(Policy = "epi:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var entrega = await _mediator.Send(new ObterEntregaEpiPorIdQuery(id), ct);
        return entrega is null ? NotFound() : Ok(entrega);
    }

    [Authorize(Policy = "epi:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarEntregaEpiCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }

    [Authorize(Policy = "epi:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarEntregaEpiCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "epi:editar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirEntregaEpiCommand(id), ct);
        return NoContent();
    }
}

using AAHBRANT.SST.Application.Perigos.Commands;
using AAHBRANT.SST.Application.Perigos.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PerigosController : ControllerBase
{
    private readonly IMediator _mediator;

    public PerigosController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "risco:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
        => Ok(await _mediator.Send(new ListarPerigosQuery(), ct));

    [Authorize(Policy = "risco:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var perigo = await _mediator.Send(new ObterPerigoPorIdQuery(id), ct);
        return perigo is null ? NotFound() : Ok(perigo);
    }

    [Authorize(Policy = "risco:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarPerigoCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }

    [Authorize(Policy = "risco:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarPerigoCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "risco:editar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirPerigoCommand(id), ct);
        return NoContent();
    }
}

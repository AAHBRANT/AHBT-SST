using AAHBRANT.SST.Application.Asos.Commands;
using AAHBRANT.SST.Application.Asos.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AsosController : ControllerBase
{
    private readonly IMediator _mediator;

    public AsosController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "aso:ver_status")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? trabalhadorId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarAsosQuery(trabalhadorId), ct));

    [Authorize(Policy = "aso:ver_status")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var aso = await _mediator.Send(new ObterAsoPorIdQuery(id), ct);
        return aso is null ? NotFound() : Ok(aso);
    }

    [Authorize(Policy = "aso:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarAsoCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }

    [Authorize(Policy = "aso:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarAsoCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "aso:editar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirAsoCommand(id), ct);
        return NoContent();
    }
}

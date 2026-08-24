using AAHBRANT.SST.Application.AreasSst.Commands;
using AAHBRANT.SST.Application.AreasSst.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AreasSstController : ControllerBase
{
    private readonly IMediator _mediator;

    public AreasSstController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "identificacao:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarAreasSstQuery(obraId), ct));

    [Authorize(Policy = "identificacao:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var area = await _mediator.Send(new ObterAreaSstPorIdQuery(id), ct);
        return area is null ? NotFound() : Ok(area);
    }

    [Authorize(Policy = "identificacao:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarAreaSstCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }

    [Authorize(Policy = "identificacao:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarAreaSstCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "identificacao:editar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirAreaSstCommand(id), ct);
        return NoContent();
    }
}

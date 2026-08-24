using AAHBRANT.SST.Application.Riscos.Commands;
using AAHBRANT.SST.Application.Riscos.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RiscosController : ControllerBase
{
    private readonly IMediator _mediator;

    public RiscosController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "risco:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? atividadeId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarRiscosQuery(atividadeId), ct));

    [Authorize(Policy = "risco:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var risco = await _mediator.Send(new ObterRiscoPorIdQuery(id), ct);
        return risco is null ? NotFound() : Ok(risco);
    }

    [Authorize(Policy = "risco:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarRiscoCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }

    [Authorize(Policy = "risco:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarRiscoCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "risco:editar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirRiscoCommand(id), ct);
        return NoContent();
    }
}

using AAHBRANT.SST.Application.Setores.Commands;
using AAHBRANT.SST.Application.Setores.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SetoresController : ControllerBase
{
    private readonly IMediator _mediator;

    public SetoresController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "organizacional:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarSetoresQuery(obraId), ct));

    [Authorize(Policy = "organizacional:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var setor = await _mediator.Send(new ObterSetorPorIdQuery(id), ct);
        return setor is null ? NotFound() : Ok(setor);
    }

    [Authorize(Policy = "organizacional:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarSetorCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }

    [Authorize(Policy = "organizacional:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarSetorCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "organizacional:excluir")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirSetorCommand(id), ct);
        return NoContent();
    }
}

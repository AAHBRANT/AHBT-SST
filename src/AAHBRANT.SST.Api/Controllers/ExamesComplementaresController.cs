using AAHBRANT.SST.Application.ExamesComplementares.Commands;
using AAHBRANT.SST.Application.ExamesComplementares.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExamesComplementaresController : ControllerBase
{
    private readonly IMediator _mediator;

    public ExamesComplementaresController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "examecomplementar:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? trabalhadorId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarExamesComplementaresQuery(trabalhadorId), ct));

    [Authorize(Policy = "examecomplementar:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var exame = await _mediator.Send(new ObterExameComplementarPorIdQuery(id), ct);
        return exame is null ? NotFound() : Ok(exame);
    }

    [Authorize(Policy = "examecomplementar:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarExameComplementarCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }

    [Authorize(Policy = "examecomplementar:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarExameComplementarCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "examecomplementar:editar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirExameComplementarCommand(id), ct);
        return NoContent();
    }
}

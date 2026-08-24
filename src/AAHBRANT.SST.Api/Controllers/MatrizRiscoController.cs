using AAHBRANT.SST.Application.MatrizRisco.Commands;
using AAHBRANT.SST.Application.MatrizRisco.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MatrizRiscoController : ControllerBase
{
    private readonly IMediator _mediator;

    public MatrizRiscoController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "risco:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
        => Ok(await _mediator.Send(new ListarMatrizRiscoConfigsQuery(), ct));

    [Authorize(Policy = "risco:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var config = await _mediator.Send(new ObterMatrizRiscoConfigPorIdQuery(id), ct);
        return config is null ? NotFound() : Ok(config);
    }

    [Authorize(Policy = "risco:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarMatrizRiscoConfigCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }

    [Authorize(Policy = "risco:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarMatrizRiscoConfigCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "risco:editar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirMatrizRiscoConfigCommand(id), ct);
        return NoContent();
    }
}

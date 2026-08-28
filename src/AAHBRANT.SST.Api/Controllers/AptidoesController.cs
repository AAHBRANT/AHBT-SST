using AAHBRANT.SST.Application.Aptidoes.Commands;
using AAHBRANT.SST.Application.Aptidoes.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AptidoesController : ControllerBase
{
    private readonly IMediator _mediator;

    public AptidoesController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "aptidao:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? trabalhadorId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarAptidoesQuery(trabalhadorId), ct));

    [Authorize(Policy = "aptidao:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var aptidao = await _mediator.Send(new ObterAptidaoPorIdQuery(id), ct);
        return aptidao is null ? NotFound() : Ok(aptidao);
    }

    [Authorize(Policy = "aptidao:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarAptidaoCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }

    [Authorize(Policy = "aptidao:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarAptidaoCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "aptidao:editar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirAptidaoCommand(id), ct);
        return NoContent();
    }
}

using AAHBRANT.SST.Application.Treinamentos.Commands;
using AAHBRANT.SST.Application.Treinamentos.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TreinamentosController : ControllerBase
{
    private readonly IMediator _mediator;

    public TreinamentosController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "treinamento:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? trabalhadorId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarTreinamentosQuery(trabalhadorId), ct));

    [Authorize(Policy = "treinamento:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var treinamento = await _mediator.Send(new ObterTreinamentoPorIdQuery(id), ct);
        return treinamento is null ? NotFound() : Ok(treinamento);
    }

    [Authorize(Policy = "treinamento:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarTreinamentoCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }

    [Authorize(Policy = "treinamento:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarTreinamentoCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "treinamento:editar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirTreinamentoCommand(id), ct);
        return NoContent();
    }
}

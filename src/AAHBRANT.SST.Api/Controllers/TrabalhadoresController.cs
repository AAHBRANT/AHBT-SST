using AAHBRANT.SST.Application.Trabalhadores.Commands;
using AAHBRANT.SST.Application.Trabalhadores.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TrabalhadoresController : ControllerBase
{
    private readonly IMediator _mediator;

    public TrabalhadoresController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "trabalhador:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarTrabalhadoresQuery(obraId), ct));

    [Authorize(Policy = "trabalhador:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var trabalhador = await _mediator.Send(new ObterTrabalhadorPorIdQuery(id), ct);
        return trabalhador is null ? NotFound() : Ok(trabalhador);
    }

    [Authorize(Policy = "trabalhador:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarTrabalhadorCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }

    [Authorize(Policy = "trabalhador:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarTrabalhadorCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "trabalhador:excluir")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirTrabalhadorCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = "trabalhador:telegram")]
    [HttpPost("{id:guid}/telegram/vinculo")]
    public async Task<IActionResult> GerarVinculoTelegram(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new GerarVinculoTelegramCommand(id), ct));
}

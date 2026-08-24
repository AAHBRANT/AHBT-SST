using AAHBRANT.SST.Application.Aprs.Commands;
using AAHBRANT.SST.Application.Aprs.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AprsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AprsController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "apr:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? atividadeId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarAprsQuery(atividadeId), ct));

    [Authorize(Policy = "apr:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterDetalhe(Guid id, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterAprDetalheQuery(id), ct);
        return detalhe is null ? NotFound() : Ok(detalhe);
    }

    [Authorize(Policy = "apr:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarAprCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterDetalhe), new { id }, new { id });
    }

    [Authorize(Policy = "apr:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarAprCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "apr:editar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirAprCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = "apr:aprovar")]
    [HttpPost("{id:guid}/aprovar")]
    public async Task<IActionResult> Aprovar(Guid id, AprovarAprRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new AprovarAprCommand(id, body.AprovadoPorUsuarioId), ct);
        return NoContent();
    }

    [Authorize(Policy = "apr:aprovar")]
    [HttpPost("{id:guid}/reprovar")]
    public async Task<IActionResult> Reprovar(Guid id, ReprovarAprRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new ReprovarAprCommand(id, body.Motivo), ct);
        return NoContent();
    }
}

public record AprovarAprRequestBody(Guid AprovadoPorUsuarioId);
public record ReprovarAprRequestBody(string Motivo);

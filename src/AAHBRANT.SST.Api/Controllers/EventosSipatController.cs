using AAHBRANT.SST.Application.Cipa.Commands;
using AAHBRANT.SST.Application.Cipa.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventosSipatController : ControllerBase
{
    private readonly IMediator _mediator;
    public EventosSipatController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "cipa:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarEventosSipatQuery(obraId), ct));

    [Authorize(Policy = "cipa:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterDetalhe(Guid id, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterEventoSipatDetalheQuery(id), ct);
        return detalhe is null ? NotFound() : Ok(detalhe);
    }

    [Authorize(Policy = "cipa:gerenciar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarEventoSipatCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterDetalhe), new { id }, new { id });
    }

    [Authorize(Policy = "cipa:gerenciar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirEventoSipatCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = "cipa:gerenciar")]
    [HttpPost("{id:guid}/atividades")]
    public async Task<IActionResult> CriarAtividade(Guid id, CriarAtividadeSipatRequestBody body, CancellationToken ct)
    {
        var atividadeId = await _mediator.Send(new CriarAtividadeSipatCommand(id, body.Data, body.Horario, body.TemaPalestra, body.Palestrante), ct);
        return Ok(new { id = atividadeId });
    }
}

public record CriarAtividadeSipatRequestBody(DateTime Data, string? Horario, string TemaPalestra, string? Palestrante);

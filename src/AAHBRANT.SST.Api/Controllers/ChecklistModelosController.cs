using AAHBRANT.SST.Application.ChecklistModelos.Commands;
using AAHBRANT.SST.Application.ChecklistModelos.Queries;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChecklistModelosController : ControllerBase
{
    private readonly IMediator _mediator;

    public ChecklistModelosController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "checklist:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] TipoInspecao? tipoInspecao, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarChecklistModelosQuery(tipoInspecao), ct));

    [Authorize(Policy = "checklist:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterDetalhe(Guid id, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterChecklistModeloDetalheQuery(id), ct);
        return detalhe is null ? NotFound() : Ok(detalhe);
    }

    [Authorize(Policy = "checklist:gerenciar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarChecklistModeloCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterDetalhe), new { id }, new { id });
    }

    [Authorize(Policy = "checklist:gerenciar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirChecklistModeloCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = "checklist:gerenciar")]
    [HttpPost("{id:guid}/novaVersao")]
    public async Task<IActionResult> NovaVersao(Guid id, NovaVersaoChecklistModeloRequestBody body, CancellationToken ct)
    {
        var novoId = await _mediator.Send(new CriarNovaVersaoChecklistModeloCommand(id, body.Itens), ct);
        return CreatedAtAction(nameof(ObterDetalhe), new { id = novoId }, new { id = novoId });
    }
}

public record NovaVersaoChecklistModeloRequestBody(List<CriarChecklistModeloItemInput> Itens);

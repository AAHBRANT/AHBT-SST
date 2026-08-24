using AAHBRANT.SST.Application.PermissaoTrabalhoRequisitos.Commands;
using AAHBRANT.SST.Application.PermissaoTrabalhoRequisitos.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PermissaoTrabalhoRequisitosController : ControllerBase
{
    private readonly IMediator _mediator;

    public PermissaoTrabalhoRequisitosController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "pt:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid permissaoTrabalhoId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarPermissaoTrabalhoRequisitosQuery(permissaoTrabalhoId), ct));

    [Authorize(Policy = "pt:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarPermissaoTrabalhoRequisitoCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(Listar), new { permissaoTrabalhoId = command.PermissaoTrabalhoId }, new { id });
    }

    [Authorize(Policy = "pt:editar")]
    [HttpPost("{id:guid}/marcar")]
    public async Task<IActionResult> Marcar(Guid id, MarcarPermissaoTrabalhoRequisitoRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new MarcarPermissaoTrabalhoRequisitoCommand(id, body.Atendido), ct);
        return NoContent();
    }

    [Authorize(Policy = "pt:editar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirPermissaoTrabalhoRequisitoCommand(id), ct);
        return NoContent();
    }
}

public record MarcarPermissaoTrabalhoRequisitoRequestBody(bool Atendido);

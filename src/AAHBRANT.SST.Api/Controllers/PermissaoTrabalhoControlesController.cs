using AAHBRANT.SST.Application.PermissaoTrabalhoControles.Commands;
using AAHBRANT.SST.Application.PermissaoTrabalhoControles.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PermissaoTrabalhoControlesController : ControllerBase
{
    private readonly IMediator _mediator;

    public PermissaoTrabalhoControlesController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "pt:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid permissaoTrabalhoId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarPermissaoTrabalhoControlesQuery(permissaoTrabalhoId), ct));

    [Authorize(Policy = "pt:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarPermissaoTrabalhoControleCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(Listar), new { permissaoTrabalhoId = command.PermissaoTrabalhoId }, new { id });
    }

    [Authorize(Policy = "pt:editar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirPermissaoTrabalhoControleCommand(id), ct);
        return NoContent();
    }
}

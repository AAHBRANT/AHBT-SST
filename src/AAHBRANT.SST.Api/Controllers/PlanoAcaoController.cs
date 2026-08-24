using AAHBRANT.SST.Application.PlanoAcao.Commands;
using AAHBRANT.SST.Application.PlanoAcao.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlanoAcaoController : ControllerBase
{
    private readonly IMediator _mediator;

    public PlanoAcaoController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "pgr:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid pgrId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarPlanoAcaoItensQuery(pgrId), ct));

    [Authorize(Policy = "pgr:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarPlanoAcaoItemCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(Listar), new { pgrId = command.PgrId }, new { id });
    }

    [Authorize(Policy = "pgr:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarPlanoAcaoItemCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "pgr:editar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirPlanoAcaoItemCommand(id), ct);
        return NoContent();
    }
}

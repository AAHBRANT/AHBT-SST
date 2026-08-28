using AAHBRANT.SST.Application.Pcmsos.Commands;
using AAHBRANT.SST.Application.Pcmsos.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PcmsosController : ControllerBase
{
    private readonly IMediator _mediator;

    public PcmsosController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "pcmso:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarPcmsosQuery(obraId), ct));

    [Authorize(Policy = "pcmso:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var pcmso = await _mediator.Send(new ObterPcmsoPorIdQuery(id), ct);
        return pcmso is null ? NotFound() : Ok(pcmso);
    }

    [Authorize(Policy = "pcmso:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarPcmsoCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }

    [Authorize(Policy = "pcmso:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarPcmsoCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "pcmso:editar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirPcmsoCommand(id), ct);
        return NoContent();
    }
}

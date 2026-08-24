using AAHBRANT.SST.Application.Atividades.Commands;
using AAHBRANT.SST.Application.Atividades.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AtividadesController : ControllerBase
{
    private readonly IMediator _mediator;

    public AtividadesController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "risco:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarAtividadesQuery(obraId), ct));

    [Authorize(Policy = "risco:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var atividade = await _mediator.Send(new ObterAtividadePorIdQuery(id), ct);
        return atividade is null ? NotFound() : Ok(atividade);
    }

    [Authorize(Policy = "risco:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarAtividadeCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }

    [Authorize(Policy = "risco:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarAtividadeCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "risco:editar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirAtividadeCommand(id), ct);
        return NoContent();
    }
}

using AAHBRANT.SST.Application.Ativos.Commands;
using AAHBRANT.SST.Application.Ativos.Queries;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AtivosController : ControllerBase
{
    private readonly IMediator _mediator;

    public AtivosController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "ativo:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? obraId, [FromQuery] TipoAtivo? tipoAtivo, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarAtivosSstQuery(obraId, tipoAtivo), ct));

    [Authorize(Policy = "ativo:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var ativo = await _mediator.Send(new ObterAtivoSstPorIdQuery(id), ct);
        return ativo is null ? NotFound() : Ok(ativo);
    }

    [Authorize(Policy = "ativo:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarAtivoSstCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }

    [Authorize(Policy = "ativo:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarAtivoSstCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "ativo:excluir")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirAtivoSstCommand(id), ct);
        return NoContent();
    }
}

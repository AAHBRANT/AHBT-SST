using AAHBRANT.SST.Application.CatalogosEpi.Commands;
using AAHBRANT.SST.Application.CatalogosEpi.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CatalogosEpiController : ControllerBase
{
    private readonly IMediator _mediator;

    public CatalogosEpiController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "epi:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
        => Ok(await _mediator.Send(new ListarCatalogosEpiQuery(), ct));

    [Authorize(Policy = "epi:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var epi = await _mediator.Send(new ObterCatalogoEpiPorIdQuery(id), ct);
        return epi is null ? NotFound() : Ok(epi);
    }

    [Authorize(Policy = "epi:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarCatalogoEpiCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }

    [Authorize(Policy = "epi:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarCatalogoEpiCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "epi:editar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirCatalogoEpiCommand(id), ct);
        return NoContent();
    }
}

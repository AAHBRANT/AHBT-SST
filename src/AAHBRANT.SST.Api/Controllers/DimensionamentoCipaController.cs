using AAHBRANT.SST.Application.Cipa.Commands;
using AAHBRANT.SST.Application.Cipa.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DimensionamentoCipaController : ControllerBase
{
    private readonly IMediator _mediator;
    public DimensionamentoCipaController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "cipa:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarDimensionamentosCipaQuery(obraId), ct));

    [Authorize(Policy = "cipa:gerenciar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarDimensionamentoCipaCommand command, CancellationToken ct)
        => Ok(new { id = await _mediator.Send(command, ct) });

    [Authorize(Policy = "cipa:gerenciar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirDimensionamentoCipaCommand(id), ct);
        return NoContent();
    }
}

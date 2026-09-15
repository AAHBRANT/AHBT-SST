using AAHBRANT.SST.Application.Alojamentos.Commands;
using AAHBRANT.SST.Application.Alojamentos.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AlojamentosController : ControllerBase
{
    private readonly IMediator _mediator;
    public AlojamentosController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "alojamento:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? obraId, CancellationToken ct)
    {
        var resultado = await _mediator.Send(new ListarAlojamentosQuery(obraId), ct);
        return Ok(resultado);
    }

    [Authorize(Policy = "alojamento:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarAlojamentoCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(Listar), new { obraId = command.ObraId }, new { id });
    }
}

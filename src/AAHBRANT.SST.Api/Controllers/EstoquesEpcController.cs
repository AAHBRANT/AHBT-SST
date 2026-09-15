using AAHBRANT.SST.Application.EstoquesEpc.Commands;
using AAHBRANT.SST.Application.EstoquesEpc.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EstoquesEpcController : ControllerBase
{
    private readonly IMediator _mediator;

    public EstoquesEpcController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "epc:ver")]
    [HttpGet("obra/{obraId:guid}")]
    public async Task<IActionResult> ListarPorObra(Guid obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarEstoqueEpcPorObraQuery(obraId), ct));

    [Authorize(Policy = "epc:ver")]
    [HttpGet("obra/{obraId:guid}/epc/{catalogoEpcId:guid}/movimentacoes")]
    public async Task<IActionResult> ListarMovimentacoes(Guid obraId, Guid catalogoEpcId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarMovimentacoesEstoqueEpcQuery(catalogoEpcId, obraId), ct));

    [Authorize(Policy = "epc:criar")]
    [HttpPost("entrada")]
    public async Task<IActionResult> RegistrarEntrada(RegistrarEntradaEstoqueEpcCommand command, CancellationToken ct)
    {
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "epc:editar")]
    [HttpPost("ajuste")]
    public async Task<IActionResult> Ajustar(AjustarEstoqueEpcCommand command, CancellationToken ct)
    {
        await _mediator.Send(command, ct);
        return NoContent();
    }
}

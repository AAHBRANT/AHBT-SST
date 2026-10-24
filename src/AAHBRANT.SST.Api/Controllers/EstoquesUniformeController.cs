using AAHBRANT.SST.Application.EstoquesUniforme.Commands;
using AAHBRANT.SST.Application.EstoquesUniforme.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EstoquesUniformeController : ControllerBase
{
    private readonly IMediator _mediator;
    public EstoquesUniformeController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "uniforme:ver")]
    [HttpGet("obra/{obraId:guid}")]
    public async Task<IActionResult> ListarPorObra(Guid obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarEstoqueUniformePorObraQuery(obraId), ct));

    [Authorize(Policy = "uniforme:ver")]
    [HttpGet("obra/{obraId:guid}/peca/{catalogoUniformeId:guid}/tamanho/{tamanho}/movimentacoes")]
    public async Task<IActionResult> ListarMovimentacoes(Guid obraId, Guid catalogoUniformeId, string tamanho, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarMovimentacoesEstoqueUniformeQuery(catalogoUniformeId, obraId, tamanho), ct));

    [Authorize(Policy = "uniforme:criar")]
    [HttpPost("entrada")]
    public async Task<IActionResult> RegistrarEntrada(RegistrarEntradaEstoqueUniformeCommand command, CancellationToken ct)
    {
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "uniforme:editar")]
    [HttpPost("ajuste")]
    public async Task<IActionResult> Ajustar(AjustarEstoqueUniformeCommand command, CancellationToken ct)
    {
        await _mediator.Send(command, ct);
        return NoContent();
    }
}

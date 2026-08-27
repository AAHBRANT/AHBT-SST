using AAHBRANT.SST.Application.EstoquesEpi.Commands;
using AAHBRANT.SST.Application.EstoquesEpi.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

// Fase 3 da reformulação do módulo EPI — estoque segmentado por Obra. Reaproveita as permissões
// já existentes do módulo EPI (não há permissão "estoque:*" separada): epi:ver para consulta,
// epi:criar para entrada manual, epi:editar para ajuste (correção de saldo).
[ApiController]
[Route("api/[controller]")]
public class EstoquesEpiController : ControllerBase
{
    private readonly IMediator _mediator;

    public EstoquesEpiController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "epi:ver")]
    [HttpGet("obra/{obraId:guid}")]
    public async Task<IActionResult> ListarPorObra(Guid obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarEstoqueEpiPorObraQuery(obraId), ct));

    [Authorize(Policy = "epi:ver")]
    [HttpGet("obra/{obraId:guid}/epi/{catalogoEpiId:guid}/movimentacoes")]
    public async Task<IActionResult> ListarMovimentacoes(Guid obraId, Guid catalogoEpiId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarMovimentacoesEstoqueEpiQuery(catalogoEpiId, obraId), ct));

    [Authorize(Policy = "epi:criar")]
    [HttpPost("entrada")]
    public async Task<IActionResult> RegistrarEntrada(RegistrarEntradaEstoqueEpiCommand command, CancellationToken ct)
    {
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "epi:editar")]
    [HttpPost("ajuste")]
    public async Task<IActionResult> Ajustar(AjustarEstoqueEpiCommand command, CancellationToken ct)
    {
        await _mediator.Send(command, ct);
        return NoContent();
    }
}

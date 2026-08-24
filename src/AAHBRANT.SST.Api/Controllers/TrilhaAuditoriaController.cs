using AAHBRANT.SST.Application.Auditoria;
using AAHBRANT.SST.Application.Auditoria.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

// Somente leitura: trilha append-only, sem endpoint de escrita (ver AcessoConfiguracoes /
// docs/RBAC-Matrix.md — nem UPDATE/DELETE são permitidos a nível de permissão de banco).
[ApiController]
[Route("api/[controller]")]
public class TrilhaAuditoriaController : ControllerBase
{
    private readonly IMediator _mediator;

    public TrilhaAuditoriaController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "auditoria:ver_trilha")]
    [HttpGet]
    public async Task<ActionResult<List<TrilhaAuditoriaDto>>> Listar(
        [FromQuery] string? entidadeTipo,
        [FromQuery] Guid? entidadeId,
        [FromQuery] Guid? usuarioId,
        [FromQuery] DateTime? dataInicio,
        [FromQuery] DateTime? dataFim)
    {
        var resultado = await _mediator.Send(
            new ListarTrilhaAuditoriaQuery(entidadeTipo, entidadeId, usuarioId, dataInicio, dataFim));
        return Ok(resultado);
    }
}

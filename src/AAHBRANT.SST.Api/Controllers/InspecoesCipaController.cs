using AAHBRANT.SST.Application.Cipa.Commands;
using AAHBRANT.SST.Application.Cipa.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InspecoesCipaController : ControllerBase
{
    private readonly IMediator _mediator;
    public InspecoesCipaController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "cipa:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarInspecoesCipaQuery(obraId), ct));

    [Authorize(Policy = "cipa:gerenciar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarInspecaoCipaCommand command, CancellationToken ct)
        => Ok(new { id = await _mediator.Send(command, ct) });

    [Authorize(Policy = "cipa:gerenciar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirInspecaoCipaCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = "nc:criar")]
    [HttpPost("{id:guid}/gerar-nao-conformidade")]
    public async Task<IActionResult> GerarNaoConformidade(Guid id, GerarNaoConformidadeDeInspecaoCipaRequestBody body, CancellationToken ct)
        => Ok(new { id = await _mediator.Send(new GerarNaoConformidadeDeInspecaoCipaCommand(id, body.ResponsavelUsuarioId, body.Prazo), ct) });
}

public record GerarNaoConformidadeDeInspecaoCipaRequestBody(Guid? ResponsavelUsuarioId, DateTime? Prazo);

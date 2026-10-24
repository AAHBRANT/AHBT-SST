using AAHBRANT.SST.Application.ExamesFuncaoObra;
using AAHBRANT.SST.Application.Ghes.Commands;
using AAHBRANT.SST.Application.Ghes.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

// Estrutura de SST da obra vinda do PGR (GHE → funções → riscos) e do PCMSO (exames por função).
[ApiController]
[Route("api/obras/{obraId:guid}")]
public class GhesController : ControllerBase
{
    private readonly IMediator _mediator;

    public GhesController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "risco:ver")]
    [HttpGet("ghes")]
    public async Task<IActionResult> ListarGhes(Guid obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarGhesObraQuery(obraId), ct));

    [Authorize(Policy = "pcmso:ver")]
    [HttpGet("exames-funcao")]
    public async Task<IActionResult> ListarExamesFuncao(Guid obraId, [FromQuery] Guid? funcaoId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarExamesFuncaoObraQuery(obraId, funcaoId), ct));

    // Importação da estrutura transcrita do PGR + PCMSO. Exige criar risco e PGR (mexe nos dois).
    [Authorize(Policy = "risco:criar")]
    [Authorize(Policy = "pgr:criar")]
    [HttpPost("estrutura-sst/importar")]
    public async Task<IActionResult> Importar(Guid obraId, ImportarEstruturaSstObraCommand command, CancellationToken ct)
        => Ok(await _mediator.Send(command with { ObraId = obraId }, ct));
}

using AAHBRANT.SST.Application.Terceirizados.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/terceirizados")]
public class TerceirizadosController : ControllerBase
{
    private readonly IMediator _mediator;
    public TerceirizadosController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "terceirizado:ver")]
    [HttpGet("pessoas")]
    public async Task<IActionResult> ListarPessoas([FromQuery] Guid? empresaId, [FromQuery] Guid? contratoId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarPessoasTerceirizadasQuery(empresaId, contratoId), ct));

    [Authorize(Policy = "terceirizado:ver")]
    [HttpGet("pessoas/{trabalhadorId:guid}/status")]
    public async Task<IActionResult> ObterStatus(Guid trabalhadorId, CancellationToken ct)
        => Ok(await _mediator.Send(new ObterStatusLiberacaoTrabalhadorQuery(trabalhadorId), ct));

    [Authorize(Policy = "terceirizado:ver")]
    [HttpGet("pendencias")]
    public async Task<IActionResult> ListarPendencias(CancellationToken ct)
        => Ok(await _mediator.Send(new ListarPendenciasTerceirizadoQuery(), ct));
}

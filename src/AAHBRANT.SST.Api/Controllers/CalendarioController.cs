using System.Security.Claims;
using AAHBRANT.SST.Application.Calendario.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

// "Quero o calendário dentro do aplicativo, tem que ser o Teams" (requisito do usuário, 2026-08-29)
// — endpoint pessoal (sempre o calendário de quem está logado, nunca de terceiros: o
// AzureAdObjectId vem do próprio token, não de um parâmetro de rota/query).
[ApiController]
[Route("api/calendario")]
public class CalendarioController : ControllerBase
{
    private readonly IMediator _mediator;

    public CalendarioController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "calendario:ver")]
    [HttpGet]
    public async Task<IActionResult> Obter([FromQuery] DateTime inicio, [FromQuery] DateTime fim, CancellationToken ct)
    {
        var azureAdObjectId = User.FindFirst("oid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Ok(await _mediator.Send(new ObterCalendarioQuery(azureAdObjectId, inicio, fim), ct));
    }
}

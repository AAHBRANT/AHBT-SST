using System.Security.Claims;
using AAHBRANT.SST.Application.Calendario.Commands;
using AAHBRANT.SST.Application.Calendario.Queries;
using AAHBRANT.SST.Application.Common.Interfaces;
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

    // Nova reunião do Teams criada pelo Calendário (09/11/2026). O organizador é quem está logado.
    [Authorize(Policy = "calendario:ver")]
    [HttpPost("reunioes")]
    public async Task<IActionResult> CriarReuniao([FromBody] CriarReuniaoCalendarioPayload corpo, CancellationToken ct)
    {
        var azureAdObjectId = User.FindFirst("oid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Ok(await _mediator.Send(new CriarReuniaoCalendarioCommand(
            azureAdObjectId, corpo.Titulo, corpo.Pauta, corpo.Inicio, corpo.Fim,
            corpo.ConvidadosUsuarioIds ?? [], corpo.EmailsExternos ?? [], corpo.ObraId, corpo.Repetir, corpo.RepetirAte), ct));
    }

    [Authorize(Policy = "calendario:ver")]
    [HttpGet("convidaveis")]
    public async Task<IActionResult> Convidaveis(CancellationToken ct)
        => Ok(await _mediator.Send(new ListarConvidaveisReuniaoQuery(), ct));
}

// Inicio/Fim no horário de Brasília, sem fuso ("2026-11-12T09:00:00").
public record CriarReuniaoCalendarioPayload(
    string Titulo, string? Pauta, DateTime Inicio, DateTime Fim,
    List<Guid>? ConvidadosUsuarioIds, List<string>? EmailsExternos, Guid? ObraId,
    TipoRecorrenciaReuniao? Repetir = null, DateOnly? RepetirAte = null);

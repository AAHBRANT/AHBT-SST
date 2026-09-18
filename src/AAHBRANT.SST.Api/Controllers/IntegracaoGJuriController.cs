using AAHBRANT.SST.Application.Terceirizados.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

// Canal novo, unidirecional G-Juri -> SST (docs/superpowers/specs/2026-09-18-modulo-terceirizado-
// design.md §5) — o G-Juri chama este webhook quando um contrato de terceirizada é validado ou
// encerrado. Autenticação via Entra ID App Role client-credentials (Sst.ReceberContratosGJuri, ver
// AppRolesReconhecidas), mesmo mecanismo já usado por IntegracaoGrhController.
[ApiController]
[Route("api/integracoes/gjuri")]
public class IntegracaoGJuriController : ControllerBase
{
    private readonly IMediator _mediator;
    public IntegracaoGJuriController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "terceirizado:integracao-gjuri")]
    [HttpPost("contratos/validados")]
    public async Task<IActionResult> ContratoValidado(ContratoValidadoWebhookCommand command, CancellationToken ct)
    {
        var contratoId = await _mediator.Send(command, ct);
        return Ok(new { contratoId });
    }

    [Authorize(Policy = "terceirizado:integracao-gjuri")]
    [HttpPost("contratos/encerrados")]
    public async Task<IActionResult> ContratoEncerrado(ContratoEncerradoWebhookCommand command, CancellationToken ct)
    {
        await _mediator.Send(command, ct);
        return NoContent();
    }
}

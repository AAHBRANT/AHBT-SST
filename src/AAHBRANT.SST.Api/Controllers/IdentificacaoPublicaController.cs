using AAHBRANT.SST.Application.AreasSst.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

// NTAG.md §3.B.4 — "View Contextual Pública": rota que precisa continuar acessível sem login ao
// escanear NFC/QR em campo. [AllowAnonymous] garante isso mesmo depois que a autenticação Entra ID
// for habilitada em Program.cs (hoje ainda desligada em ambiente de desenvolvimento).
[ApiController]
[AllowAnonymous]
[Route("sst/p")]
public class IdentificacaoPublicaController : ControllerBase
{
    private readonly IMediator _mediator;

    public IdentificacaoPublicaController(IMediator mediator) => _mediator = mediator;

    [HttpGet("{codigoOuUid}")]
    public async Task<IActionResult> Resolver(string codigoOuUid, CancellationToken ct)
    {
        var area = await _mediator.Send(new ResolverAreaPublicaQuery(codigoOuUid), ct);
        return area is null ? NotFound() : Ok(area);
    }
}

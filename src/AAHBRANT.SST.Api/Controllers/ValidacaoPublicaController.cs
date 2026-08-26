using AAHBRANT.SST.Application.Assinatura.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

// Motor de Assinatura Eletrônica (docs/Motor-Assinatura-Eletronica.md §5, etapa 11) — mesmo padrão de
// IdentificacaoPublicaController: rota que precisa continuar acessível sem login para quem escaneia o
// QR do comprovante em campo (pode não estar logado nem no Teams).
[ApiController]
[AllowAnonymous]
[Route("sst/validar")]
public class ValidacaoPublicaController : ControllerBase
{
    private readonly IMediator _mediator;

    public ValidacaoPublicaController(IMediator mediator) => _mediator = mediator;

    [HttpGet("{token}")]
    public async Task<IActionResult> Resolver(string token, CancellationToken ct)
    {
        var documento = await _mediator.Send(new ResolverDocumentoPublicoQuery(token), ct);
        return documento is null ? NotFound() : Ok(documento);
    }
}

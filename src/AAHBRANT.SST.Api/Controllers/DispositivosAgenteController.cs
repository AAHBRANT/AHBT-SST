using AAHBRANT.SST.Application.Assinatura.Commands;
using AAHBRANT.SST.Application.Assinatura.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/dispositivos-agente")]
public class DispositivosAgenteController : ControllerBase
{
    private readonly IMediator _mediator;

    public DispositivosAgenteController(IMediator mediator)
    {
        _mediator = mediator;
    }

    public record RegistrarDispositivoAgenteRequestBody(Guid ObraId, string Nome);

    [HttpPost]
    [Authorize(Policy = "organizacional:editar")]
    public async Task<ActionResult<string>> Registrar(RegistrarDispositivoAgenteRequestBody body, CancellationToken ct)
    {
        var segredo = await _mediator.Send(new RegistrarDispositivoAgenteCommand(body.ObraId, body.Nome), ct);
        return Ok(segredo);
    }

    public record SincronizarTemplatesRequestBody(string SegredoDispositivo);

    // AllowAnonymous: este endpoint é chamado pelo agente local (sem token Entra ID), não pelo
    // navegador do quiosque. A autenticação é o segredo do dispositivo no corpo do POST, validado
    // manualmente dentro do handler via IDispositivoAgenteAutenticador — nunca em query string.
    [HttpPost("{id:guid}/templates/sincronizar")]
    [AllowAnonymous]
    public async Task<ActionResult<List<TemplateSincronizadoDto>>> Sincronizar(Guid id, SincronizarTemplatesRequestBody body, CancellationToken ct)
    {
        var templates = await _mediator.Send(new SincronizarTemplatesQuery(id, body.SegredoDispositivo), ct);
        return Ok(templates);
    }
}

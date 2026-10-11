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

    // Devolve o Id e o segredo em claro UMA vez (no banco fica só o hash): quem registra precisa
    // guardá-los para configurar o agente no PC da obra.
    [HttpPost]
    [Authorize(Policy = "organizacional:editar")]
    public async Task<ActionResult<RegistroDispositivoAgente>> Registrar(RegistrarDispositivoAgenteRequestBody body, CancellationToken ct)
        => Ok(await _mediator.Send(new RegistrarDispositivoAgenteCommand(body.ObraId, body.Nome), ct));

    [HttpGet]
    [Authorize(Policy = "organizacional:ver")]
    public async Task<ActionResult<List<DispositivoAgenteDto>>> Listar([FromQuery] Guid? obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarDispositivosAgenteQuery(obraId), ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "organizacional:excluir")]
    public async Task<IActionResult> Revogar(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new RevogarDispositivoAgenteCommand(id), ct);
        return NoContent();
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

    // O navegador pede um desafio antes de cada leitura de digital e o repassa ao agente, que o assina.
    // Qualquer usuário logado: o desafio sozinho não dá direito a nada, quem assina é o leitor.
    [HttpPost("{id:guid}/desafios")]
    [Authorize]
    public async Task<ActionResult<object>> EmitirDesafio(Guid id, CancellationToken ct)
        => Ok(new { desafio = await _mediator.Send(new EmitirDesafioBiometriaCommand(id), ct) });

    public record RegistrarChavePublicaRequestBody(string SegredoDispositivo, string ChavePublica);

    // AllowAnonymous pelo mesmo motivo da sincronização: quem chama é o agente, autenticado pelo segredo.
    [HttpPost("{id:guid}/chave-publica")]
    [AllowAnonymous]
    public async Task<IActionResult> RegistrarChavePublica(Guid id, RegistrarChavePublicaRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new RegistrarChavePublicaDispositivoCommand(id, body.SegredoDispositivo, body.ChavePublica), ct);
        return NoContent();
    }
}

using AAHBRANT.SST.Application.RegrasAlerta.Commands;
using AAHBRANT.SST.Application.RegrasAlerta.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

// Tela de administração do Motor Central de Alertas (requisito do usuário, 2026-08-25): antes só
// dava pra ajustar RegraAlerta.DiasAntecedencia/Severidade direto no banco. Este controller
// expõe o CRUD de RegraAlerta para a aba "Configurações" de AlertasPage.tsx.
[ApiController]
[Route("api/[controller]")]
public class RegrasAlertaController : ControllerBase
{
    private readonly IMediator _mediator;

    public RegrasAlertaController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "regraalerta:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
        => Ok(await _mediator.Send(new ListarRegrasAlertaQuery(), ct));

    [Authorize(Policy = "regraalerta:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarRegraAlertaCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(Listar), new { id }, new { id });
    }

    [Authorize(Policy = "regraalerta:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarRegraAlertaCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "regraalerta:excluir")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirRegraAlertaCommand(id), ct);
        return NoContent();
    }
}

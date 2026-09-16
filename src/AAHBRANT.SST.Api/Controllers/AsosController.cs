using AAHBRANT.SST.Application.Asos.Commands;
using AAHBRANT.SST.Application.Asos.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AsosController : ControllerBase
{
    private readonly IMediator _mediator;

    public AsosController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "aso:ver_status")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? trabalhadorId, [FromQuery] Guid? obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarAsosQuery(trabalhadorId, obraId), ct));

    [Authorize(Policy = "aso:ver_status")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var aso = await _mediator.Send(new ObterAsoPorIdQuery(id), ct);
        return aso is null ? NotFound() : Ok(aso);
    }

    [Authorize(Policy = "aso:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarAsoCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }

    [Authorize(Policy = "aso:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarAsoCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "aso:editar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirAsoCommand(id), ct);
        return NoContent();
    }

    // Sincronização manual sob demanda (Integração G-RH, 2026-09-16) — a rotina normal é o polling
    // automático (GrhDbPollingService); este endpoint só força uma rodada agora, útil pra testar ou
    // pra não esperar o próximo ciclo depois de completar o cadastro no G-RH.
    [Authorize(Policy = "aso:criar")]
    [HttpPost("importar-grh")]
    public async Task<IActionResult> ImportarDoGrh(CancellationToken ct)
        => Ok(await _mediator.Send(new ImportarAsoGrhCommand(), ct));
}

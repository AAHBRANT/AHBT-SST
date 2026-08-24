using AAHBRANT.SST.Application.Alertas.Commands;
using AAHBRANT.SST.Application.Alertas.Queries;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AlertasController : ControllerBase
{
    private readonly IMediator _mediator;

    public AlertasController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "alerta:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] StatusAlerta? status,
        [FromQuery] SeveridadeAlerta? severidade,
        [FromQuery] Guid? obraId,
        [FromQuery] Guid? trabalhadorId,
        CancellationToken ct)
        => Ok(await _mediator.Send(new ListarAlertasQuery(status, severidade, obraId, trabalhadorId), ct));

    [Authorize(Policy = "alerta:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var alerta = await _mediator.Send(new ObterAlertaPorIdQuery(id), ct);
        return alerta is null ? NotFound() : Ok(alerta);
    }

    [Authorize(Policy = "alerta:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarAlertaCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }

    [Authorize(Policy = "alerta:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarAlertaRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new AtualizarAlertaCommand(
            id, body.Tipo, body.Severidade, body.Titulo, body.Descricao,
            body.TrabalhadorId, body.ObraId, body.DestinatarioUsuarioId, body.DataLimiteTratamento), ct);
        return NoContent();
    }

    [Authorize(Policy = "alerta:editar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirAlertaCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = "alerta:tratar")]
    [HttpPost("{id:guid}/iniciar-tratamento")]
    public async Task<IActionResult> IniciarTratamento(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new IniciarTratamentoAlertaCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = "alerta:escalonar")]
    [HttpPost("{id:guid}/escalonar")]
    public async Task<IActionResult> Escalonar(Guid id, EscalonarAlertaRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new EscalonarAlertaCommand(id, body.EscalonadoParaUsuarioId), ct);
        return NoContent();
    }

    [Authorize(Policy = "alerta:tratar")]
    [HttpPost("{id:guid}/resolver")]
    public async Task<IActionResult> Resolver(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ResolverAlertaCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = "alerta:tratar")]
    [HttpPost("{id:guid}/ignorar")]
    public async Task<IActionResult> Ignorar(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new IgnorarAlertaCommand(id), ct);
        return NoContent();
    }
}

public record AtualizarAlertaRequestBody(
    TipoAlerta Tipo,
    SeveridadeAlerta Severidade,
    string Titulo,
    string? Descricao,
    Guid? TrabalhadorId,
    Guid? ObraId,
    Guid? DestinatarioUsuarioId,
    DateTime? DataLimiteTratamento);

public record EscalonarAlertaRequestBody(Guid EscalonadoParaUsuarioId);

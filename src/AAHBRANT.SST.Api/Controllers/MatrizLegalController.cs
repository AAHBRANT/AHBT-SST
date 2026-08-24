using AAHBRANT.SST.Application.MatrizLegal.Commands;
using AAHBRANT.SST.Application.MatrizLegal.Queries;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MatrizLegalController : ControllerBase
{
    private readonly IMediator _mediator;

    public MatrizLegalController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "matrizlegal:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string? norma,
        [FromQuery] string? tema,
        [FromQuery] bool? aplicabilidade,
        [FromQuery] StatusRequisitoLegal? status,
        [FromQuery] Guid? obraId,
        CancellationToken ct)
        => Ok(await _mediator.Send(new ListarRequisitosLegaisQuery(norma, tema, aplicabilidade, status, obraId), ct));

    [Authorize(Policy = "matrizlegal:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterDetalhe(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new ObterRequisitoLegalDetalheQuery(id), ct));

    [Authorize(Policy = "matrizlegal:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarRequisitoLegalCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterDetalhe), new { id }, new { id });
    }

    [Authorize(Policy = "matrizlegal:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarRequisitoLegalRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new AtualizarRequisitoLegalCommand(
            id, body.Codigo, body.Norma, body.Item, body.Tema, body.Requisito, body.Aplicabilidade,
            body.Justificativa, body.Evidencia, body.ResponsavelUsuarioId, body.Periodicidade,
            body.Prazo, body.UltimaRevisao, body.ProximaRevisao, body.ObraId), ct);
        return NoContent();
    }

    [Authorize(Policy = "matrizlegal:editar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirRequisitoLegalCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = "matrizlegal:atualizar_status")]
    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> AtualizarStatus(Guid id, AtualizarStatusRequisitoLegalRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new AtualizarStatusRequisitoLegalCommand(id, body.NovoStatus), ct);
        return NoContent();
    }
}

public record AtualizarRequisitoLegalRequestBody(
    string Codigo,
    string Norma,
    string? Item,
    string Tema,
    string Requisito,
    bool Aplicabilidade,
    string? Justificativa,
    string? Evidencia,
    Guid? ResponsavelUsuarioId,
    string? Periodicidade,
    DateTime? Prazo,
    DateTime? UltimaRevisao,
    DateTime? ProximaRevisao,
    Guid? ObraId);

public record AtualizarStatusRequisitoLegalRequestBody(StatusRequisitoLegal NovoStatus);

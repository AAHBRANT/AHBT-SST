using AAHBRANT.SST.Application.Cipa.Commands;
using AAHBRANT.SST.Application.Cipa.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReunioesCipaController : ControllerBase
{
    private readonly IMediator _mediator;
    public ReunioesCipaController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "cipa:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarReunioesCipaQuery(obraId), ct));

    [Authorize(Policy = "cipa:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterDetalhe(Guid id, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterReuniaoCipaDetalheQuery(id), ct);
        return detalhe is null ? NotFound() : Ok(detalhe);
    }

    [Authorize(Policy = "cipa:gerenciar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarReuniaoCipaCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterDetalhe), new { id }, new { id });
    }

    [Authorize(Policy = "cipa:gerenciar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirReuniaoCipaCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = "cipa:gerenciar")]
    [HttpPut("{id:guid}/participantes")]
    public async Task<IActionResult> RegistrarParticipantes(Guid id, List<ParticipanteReuniaoCipaEntrada> participantes, CancellationToken ct)
    {
        await _mediator.Send(new RegistrarParticipantesReuniaoCipaCommand(id, participantes), ct);
        return NoContent();
    }

    [Authorize(Policy = "cipa:gerenciar")]
    [HttpPost("{id:guid}/encerrar")]
    public async Task<IActionResult> Encerrar(Guid id, EncerrarReuniaoCipaRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new EncerrarReuniaoCipaCommand(id, body.Deliberacoes), ct);
        return NoContent();
    }

    [Authorize(Policy = "cipa:exportar")]
    [HttpGet("{id:guid}/ata-pdf")]
    public async Task<IActionResult> ExportarAta(Guid id, CancellationToken ct)
    {
        var pdf = await _mediator.Send(new ExportarAtaReuniaoCipaPdfQuery(id), ct);
        return pdf is null ? NotFound() : File(pdf, "application/pdf", $"ata-reuniao-cipa-{id}.pdf");
    }
}

public record EncerrarReuniaoCipaRequestBody(string Deliberacoes);

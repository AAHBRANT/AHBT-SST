using AAHBRANT.SST.Application.Cipa.Commands;
using AAHBRANT.SST.Application.Cipa.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProcessosEleitoraisCipaController : ControllerBase
{
    private readonly IMediator _mediator;
    public ProcessosEleitoraisCipaController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "cipa:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarProcessosEleitoraisCipaQuery(obraId), ct));

    [Authorize(Policy = "cipa:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterDetalhe(Guid id, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterProcessoEleitoralCipaDetalheQuery(id), ct);
        return detalhe is null ? NotFound() : Ok(detalhe);
    }

    [Authorize(Policy = "cipa:gerenciar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarProcessoEleitoralCipaCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterDetalhe), new { id }, new { id });
    }

    [Authorize(Policy = "cipa:gerenciar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirProcessoEleitoralCipaCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = "cipa:gerenciar")]
    [HttpPost("{id:guid}/candidatos")]
    public async Task<IActionResult> Inscrever(Guid id, InscreverCandidatoCipaRequestBody body, CancellationToken ct)
        => Ok(new { id = await _mediator.Send(new InscreverCandidatoCipaCommand(id, body.TrabalhadorId), ct) });

    [Authorize(Policy = "cipa:gerenciar")]
    [HttpPost("candidatos/{candidatoId:guid}/avaliar")]
    public async Task<IActionResult> AvaliarInscricao(Guid candidatoId, AvaliarInscricaoCandidatoCipaRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new AvaliarInscricaoCandidatoCipaCommand(candidatoId, body.Deferido, body.MotivoIndeferimento), ct);
        return NoContent();
    }

    [Authorize(Policy = "cipa:gerenciar")]
    [HttpPost("{id:guid}/apuracao")]
    public async Task<IActionResult> Apurar(Guid id, RegistrarApuracaoProcessoEleitoralCipaRequestBody body, CancellationToken ct)
    {
        var membrosIds = await _mediator.Send(new RegistrarApuracaoProcessoEleitoralCipaCommand(
            id, body.Votos, body.DataInicioMandato, body.DataFimMandato), ct);
        return Ok(new { membrosIds });
    }

    [Authorize(Policy = "cipa:exportar")]
    [HttpGet("{id:guid}/ata-pdf")]
    public async Task<IActionResult> ExportarAta(Guid id, CancellationToken ct)
    {
        var pdf = await _mediator.Send(new ExportarAtaEleicaoCipaPdfQuery(id), ct);
        return pdf is null ? NotFound() : File(pdf, "application/pdf", $"ata-eleicao-cipa-{id}.pdf");
    }
}

public record InscreverCandidatoCipaRequestBody(Guid TrabalhadorId);
public record AvaliarInscricaoCandidatoCipaRequestBody(bool Deferido, string? MotivoIndeferimento);
public record RegistrarApuracaoProcessoEleitoralCipaRequestBody(List<VotoApuradoCipa> Votos, DateTime DataInicioMandato, DateTime DataFimMandato);

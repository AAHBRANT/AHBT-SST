using AAHBRANT.SST.Application.Dds.Commands;
using AAHBRANT.SST.Application.Dds.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DdsController : ControllerBase
{
    private readonly IMediator _mediator;

    public DdsController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "dds:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarDdsQuery(obraId), ct));

    [Authorize(Policy = "dds:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterDetalhe(Guid id, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterDdsDetalheQuery(id), ct);
        return detalhe is null ? NotFound() : Ok(detalhe);
    }

    [Authorize(Policy = "dds:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarDdsCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterDetalhe), new { id }, new { id });
    }

    [Authorize(Policy = "dds:criar")]
    [HttpPost("sem-expediente")]
    public async Task<IActionResult> RegistrarSemExpediente(RegistrarDiaSemExpedienteCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterDetalhe), new { id }, new { id });
    }

    [Authorize(Policy = "dds:conduzir")]
    [HttpPost("itens/{itemId:guid}/marcar")]
    public async Task<IActionResult> MarcarItem(Guid itemId, MarcarItemChecklistRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new MarcarItemChecklistCommand(itemId, body.Verificado), ct);
        return NoContent();
    }

    [Authorize(Policy = "dds:conduzir")]
    [HttpPost("{id:guid}/participantes")]
    public async Task<IActionResult> RegistrarParticipante(Guid id, RegistrarParticipanteRequestBody body, CancellationToken ct)
    {
        var command = new RegistrarParticipanteCommand(id, body.TrabalhadorId, body.DispositivoId, body.SegredoDispositivo, body.Score);
        var participanteId = await _mediator.Send(command, ct);
        return Ok(new { id = participanteId });
    }

    [Authorize(Policy = "dds:ver")]
    [HttpGet("participantes/{participanteId:guid}/foto")]
    public async Task<IActionResult> ObterFotoParticipante(Guid participanteId, CancellationToken ct)
    {
        var foto = await _mediator.Send(new ObterFotoParticipanteQuery(participanteId), ct);
        return foto is null ? NotFound() : File(foto.Conteudo, foto.ContentType, foto.NomeArquivo);
    }

    [Authorize(Policy = "dds:encerrar")]
    [HttpPost("{id:guid}/encerrar")]
    public async Task<IActionResult> Encerrar(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new EncerrarDdsCommand(id), ct);
        return NoContent();
    }

    // Evidências fotográficas obrigatórias do registro diário (31/08, pedido do usuário: "3 fotos
    // por registro de DDS para liberação do encerramento") — mesmo padrão de
    // InspecoesController.AnexarFoto/ObterFoto.
    [Authorize(Policy = "dds:conduzir")]
    [HttpPost("{id:guid}/fotos-evidencia")]
    [RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> AnexarFotoEvidencia(Guid id, [FromForm] AnexarFotoEvidenciaDdsRequestBody body, CancellationToken ct)
    {
        await using var stream = new MemoryStream();
        await body.Foto.CopyToAsync(stream, ct);

        var fotoId = await _mediator.Send(new AnexarFotoEvidenciaDdsCommand(id, stream.ToArray(), body.Foto.ContentType), ct);
        return Ok(new { id = fotoId });
    }

    [Authorize(Policy = "dds:ver")]
    [HttpGet("fotos-evidencia/{fotoId:guid}")]
    public async Task<IActionResult> ObterFotoEvidencia(Guid fotoId, CancellationToken ct)
    {
        var foto = await _mediator.Send(new ObterFotoEvidenciaDdsQuery(fotoId), ct);
        return foto is null ? NotFound() : File(foto.Conteudo, foto.ContentType, foto.NomeArquivo);
    }

    [Authorize(Policy = "dds:exportar")]
    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> ExportarPdf(Guid id, CancellationToken ct)
    {
        var pdf = await _mediator.Send(new ExportarDdsPdfQuery(id), ct);
        return pdf is null ? NotFound() : File(pdf, "application/pdf", $"dds-{id}.pdf");
    }

    [Authorize(Policy = "dds:exportar")]
    [HttpPost("{id:guid}/telegram/enviar")]
    public async Task<IActionResult> EnviarTelegram(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new EnviarDdsTelegramCommand(id), ct));
}

public record MarcarItemChecklistRequestBody(bool Verificado);

public class RegistrarParticipanteRequestBody
{
    public Guid TrabalhadorId { get; set; }
    public Guid DispositivoId { get; set; }
    public string SegredoDispositivo { get; set; } = string.Empty;
    public double Score { get; set; }
}

public class AnexarFotoEvidenciaDdsRequestBody
{
    public IFormFile Foto { get; set; } = null!;
}

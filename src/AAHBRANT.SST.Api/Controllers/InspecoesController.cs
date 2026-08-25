using AAHBRANT.SST.Application.Inspecoes.Commands;
using AAHBRANT.SST.Application.Inspecoes.Queries;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InspecoesController : ControllerBase
{
    private readonly IMediator _mediator;

    public InspecoesController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "inspecao:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarInspecoesQuery(obraId), ct));

    [Authorize(Policy = "inspecao:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterDetalhe(Guid id, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterInspecaoDetalheQuery(id), ct);
        return detalhe is null ? NotFound() : Ok(detalhe);
    }

    [Authorize(Policy = "inspecao:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarInspecaoCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterDetalhe), new { id }, new { id });
    }

    [Authorize(Policy = "inspecao:responder")]
    [HttpPost("respostas/{respostaId:guid}")]
    public async Task<IActionResult> ResponderItem(Guid respostaId, ResponderItemInspecaoRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new ResponderItemInspecaoCommand(
            respostaId, body.StatusItem, body.Observacao, body.ResponsavelUsuarioId, body.Prazo), ct);
        return NoContent();
    }

    [Authorize(Policy = "inspecao:responder")]
    [HttpPost("respostas/{respostaId:guid}/foto")]
    [RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> AnexarFoto(Guid respostaId, [FromForm] AnexarFotoItemInspecaoRequestBody body, CancellationToken ct)
    {
        await using var stream = new MemoryStream();
        await body.Foto.CopyToAsync(stream, ct);

        await _mediator.Send(new AnexarFotoItemInspecaoCommand(respostaId, stream.ToArray(), body.Foto.ContentType), ct);
        return NoContent();
    }

    [Authorize(Policy = "inspecao:ver")]
    [HttpGet("respostas/{respostaId:guid}/foto")]
    public async Task<IActionResult> ObterFoto(Guid respostaId, CancellationToken ct)
    {
        var foto = await _mediator.Send(new ObterFotoItemInspecaoQuery(respostaId), ct);
        return foto is null ? NotFound() : File(foto.Conteudo, foto.ContentType, foto.NomeArquivo);
    }

    [Authorize(Policy = "inspecao:encerrar")]
    [HttpPost("{id:guid}/encerrar")]
    public async Task<IActionResult> Encerrar(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new EncerrarInspecaoCommand(id), ct);
        return NoContent();
    }
}

public record ResponderItemInspecaoRequestBody(
    StatusItemChecklist StatusItem,
    string? Observacao,
    Guid? ResponsavelUsuarioId,
    DateTime? Prazo);

public class AnexarFotoItemInspecaoRequestBody
{
    public IFormFile Foto { get; set; } = null!;
}

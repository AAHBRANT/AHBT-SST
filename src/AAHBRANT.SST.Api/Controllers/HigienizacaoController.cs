using AAHBRANT.SST.Application.Higienizacao.Commands;
using AAHBRANT.SST.Application.Higienizacao.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HigienizacaoController : ControllerBase
{
    private readonly IMediator _mediator;

    public HigienizacaoController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "higienizacao:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarItensHigienizacaoQuery(obraId), ct));

    [Authorize(Policy = "higienizacao:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterDetalhe(Guid id, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterItemHigienizacaoDetalheQuery(id), ct);
        return detalhe is null ? NotFound() : Ok(detalhe);
    }

    [Authorize(Policy = "higienizacao:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarItemHigienizacaoCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterDetalhe), new { id }, new { id });
    }

    [Authorize(Policy = "higienizacao:registrar")]
    [HttpPost("{id:guid}/registros")]
    [RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> RegistrarHigienizacao(Guid id, [FromForm] RegistrarHigienizacaoRequestBody body, CancellationToken ct)
    {
        await using var stream = new MemoryStream();
        await body.Foto.CopyToAsync(stream, ct);

        var command = new RegistrarHigienizacaoCommand(id, body.TrabalhadorId, body.Observacoes, stream.ToArray(), body.Foto.ContentType);
        var registroId = await _mediator.Send(command, ct);
        return Ok(new { id = registroId });
    }

    [Authorize(Policy = "higienizacao:ver")]
    [HttpGet("registros/{registroId:guid}/foto")]
    public async Task<IActionResult> ObterFotoRegistro(Guid registroId, CancellationToken ct)
    {
        var foto = await _mediator.Send(new ObterFotoRegistroHigienizacaoQuery(registroId), ct);
        return foto is null ? NotFound() : File(foto.Conteudo, foto.ContentType, foto.NomeArquivo);
    }
}

public class RegistrarHigienizacaoRequestBody
{
    public Guid TrabalhadorId { get; set; }
    public string? Observacoes { get; set; }
    public IFormFile Foto { get; set; } = null!;
}

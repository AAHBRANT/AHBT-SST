using AAHBRANT.SST.Application.PgrRevisoes.Commands;
using AAHBRANT.SST.Application.PgrRevisoes.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PgrRevisoesController : ControllerBase
{
    private readonly IMediator _mediator;

    public PgrRevisoesController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "pgr:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid pgrId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarPgrRevisoesQuery(pgrId), ct));

    [Authorize(Policy = "pgr:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarPgrRevisaoCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(Listar), new { pgrId = command.PgrId }, new { id });
    }

    // "Nova revisão" (10/10/2026): anexa o PDF de uma revisão nova sem apagar o anterior.
    [Authorize(Policy = "pgr:editar")]
    [HttpPost("nova")]
    [RequestSizeLimit(21_000_000)]
    public async Task<IActionResult> Nova([FromForm] NovaRevisaoRequestBody body, CancellationToken ct)
    {
        await using var stream = new MemoryStream();
        await body.Arquivo.CopyToAsync(stream, ct);
        var id = await _mediator.Send(new NovaRevisaoPgrCommand(
            body.DocumentoId, body.NumeroRevisao, body.DataRevisao, body.Motivo,
            stream.ToArray(), body.Arquivo.ContentType, body.Arquivo.FileName), ct);
        return Ok(new { id });
    }

    [Authorize(Policy = "pgr:ver")]
    [HttpGet("{id:guid}/documento")]
    public async Task<IActionResult> ObterDocumento(Guid id, CancellationToken ct)
    {
        var documento = await _mediator.Send(new ObterDocumentoRevisaoPgrQuery(id), ct);
        return documento is null ? NotFound() : File(documento.Conteudo, documento.ContentType, documento.NomeArquivo);
    }
}

// Corpo multipart da "Nova revisão" — usado pelo PGR (DocumentoId = PgrId) e pelo PCMSO (= PcmsoId).
public class NovaRevisaoRequestBody
{
    public Guid DocumentoId { get; set; }
    public int? NumeroRevisao { get; set; }
    public DateTime DataRevisao { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public IFormFile Arquivo { get; set; } = null!;
}

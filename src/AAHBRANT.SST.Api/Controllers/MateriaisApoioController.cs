using AAHBRANT.SST.Application.MateriaisApoio.Commands;
using AAHBRANT.SST.Application.MateriaisApoio.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MateriaisApoioController : ControllerBase
{
    private readonly IMediator _mediator;

    public MateriaisApoioController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "materialapoio:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] string? categoria, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarMateriaisApoioQuery(categoria), ct));

    [Authorize(Policy = "materialapoio:ver")]
    [HttpGet("{id:guid}/conteudo")]
    public async Task<IActionResult> ObterConteudo(Guid id, CancellationToken ct)
    {
        var material = await _mediator.Send(new ObterConteudoMaterialApoioQuery(id), ct);
        return material is null ? NotFound() : File(material.Conteudo, material.ContentType, material.NomeArquivo);
    }

    [Authorize(Policy = "materialapoio:criar")]
    [HttpPost]
    [RequestSizeLimit(21_000_000)]
    public async Task<IActionResult> Criar([FromForm] CriarMaterialApoioRequestBody body, CancellationToken ct)
    {
        await using var stream = new MemoryStream();
        await body.Arquivo.CopyToAsync(stream, ct);

        var id = await _mediator.Send(new CriarMaterialApoioCommand(
            body.Nome, body.Categoria, body.Arquivo.FileName, body.Arquivo.ContentType, stream.ToArray()), ct);
        return CreatedAtAction(nameof(ObterConteudo), new { id }, new { id });
    }

    [Authorize(Policy = "materialapoio:excluir")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirMaterialApoioCommand(id), ct);
        return NoContent();
    }
}

public class CriarMaterialApoioRequestBody
{
    public string Nome { get; set; } = string.Empty;
    public string? Categoria { get; set; }
    public IFormFile Arquivo { get; set; } = null!;
}

using AAHBRANT.SST.Domain.Enums;
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
            body.Nome, body.Categoria, body.Arquivo.FileName, body.Arquivo.ContentType, stream.ToArray(),
            body.Tipo, body.Codigo, body.Revisao, body.Situacao, body.FormaConsulta, body.IncluirEmTodasOs), ct);
        return CreatedAtAction(nameof(ObterConteudo), new { id }, new { id });
    }

    // Dados do documento controlado (situação, revisão...), sem trocar o arquivo.
    [Authorize(Policy = "materialapoio:criar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarDocumentoSstCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
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
    public TipoDocumentoSst Tipo { get; set; } = TipoDocumentoSst.MaterialApoio;
    public string? Codigo { get; set; }
    public string? Revisao { get; set; }
    public SituacaoDocumentoSst? Situacao { get; set; }
    public string? FormaConsulta { get; set; }
    public bool IncluirEmTodasOs { get; set; }
}

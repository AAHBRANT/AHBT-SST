using AAHBRANT.SST.Application.CatalogosEpc.Commands;
using AAHBRANT.SST.Application.CatalogosEpc.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CatalogosEpcController : ControllerBase
{
    private readonly IMediator _mediator;

    public CatalogosEpcController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "epc:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
        => Ok(await _mediator.Send(new ListarCatalogosEpcQuery(), ct));

    [Authorize(Policy = "epc:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var epc = await _mediator.Send(new ObterCatalogoEpcPorIdQuery(id), ct);
        return epc is null ? NotFound() : Ok(epc);
    }

    [Authorize(Policy = "epc:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarCatalogoEpcCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }

    [Authorize(Policy = "epc:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarCatalogoEpcCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "epc:editar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirCatalogoEpcCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = "epc:editar")]
    [HttpPost("{id:guid}/foto")]
    [RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> AnexarFoto(Guid id, [FromForm] AnexarFotoCatalogoEpcRequestBody body, CancellationToken ct)
    {
        await using var stream = new MemoryStream();
        await body.Foto.CopyToAsync(stream, ct);

        var command = new AnexarFotoCatalogoEpcCommand(id, stream.ToArray(), body.Foto.ContentType);
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "epc:ver")]
    [HttpGet("{id:guid}/foto")]
    public async Task<IActionResult> ObterFoto(Guid id, CancellationToken ct)
    {
        var foto = await _mediator.Send(new ObterFotoCatalogoEpcQuery(id), ct);
        return foto is null ? NotFound() : File(foto.Conteudo, foto.ContentType, foto.NomeArquivo);
    }
}

public class AnexarFotoCatalogoEpcRequestBody
{
    public IFormFile Foto { get; set; } = null!;
}

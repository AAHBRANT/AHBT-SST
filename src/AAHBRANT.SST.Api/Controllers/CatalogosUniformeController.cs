using AAHBRANT.SST.Application.CatalogosUniforme.Commands;
using AAHBRANT.SST.Application.CatalogosUniforme.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CatalogosUniformeController : ControllerBase
{
    private readonly IMediator _mediator;
    public CatalogosUniformeController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "uniforme:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
        => Ok(await _mediator.Send(new ListarCatalogosUniformeQuery(), ct));

    [Authorize(Policy = "uniforme:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var item = await _mediator.Send(new ObterCatalogoUniformePorIdQuery(id), ct);
        return item is null ? NotFound() : Ok(item);
    }

    [Authorize(Policy = "uniforme:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarCatalogoUniformeCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }

    [Authorize(Policy = "uniforme:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarCatalogoUniformeCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "uniforme:editar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirCatalogoUniformeCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = "uniforme:editar")]
    [HttpPost("{id:guid}/foto")]
    [RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> AnexarFoto(Guid id, [FromForm] AnexarFotoCatalogoUniformeRequestBody body, CancellationToken ct)
    {
        await using var stream = new MemoryStream();
        await body.Foto.CopyToAsync(stream, ct);

        var command = new AnexarFotoCatalogoUniformeCommand(id, stream.ToArray(), body.Foto.ContentType);
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "uniforme:ver")]
    [HttpGet("{id:guid}/foto")]
    public async Task<IActionResult> ObterFoto(Guid id, CancellationToken ct)
    {
        var foto = await _mediator.Send(new ObterFotoCatalogoUniformeQuery(id), ct);
        return foto is null ? NotFound() : File(foto.Conteudo, foto.ContentType, foto.NomeArquivo);
    }
}

public class AnexarFotoCatalogoUniformeRequestBody
{
    public IFormFile Foto { get; set; } = null!;
}

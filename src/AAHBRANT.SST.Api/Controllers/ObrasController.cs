using AAHBRANT.SST.Application.Obras.Commands;
using AAHBRANT.SST.Application.Obras.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ObrasController : ControllerBase
{
    private readonly IMediator _mediator;

    public ObrasController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "organizacional:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
        => Ok(await _mediator.Send(new ListarObrasQuery(), ct));

    [Authorize(Policy = "organizacional:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var obra = await _mediator.Send(new ObterObraPorIdQuery(id), ct);
        return obra is null ? NotFound() : Ok(obra);
    }

    [Authorize(Policy = "organizacional:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarObraCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }

    [Authorize(Policy = "organizacional:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarObraCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "organizacional:excluir")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirObraCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = "organizacional:editar")]
    [HttpPost("{id:guid}/logo")]
    [RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> AnexarLogo(Guid id, [FromForm] AnexarLogoObraRequestBody body, CancellationToken ct)
    {
        await using var stream = new MemoryStream();
        await body.Logo.CopyToAsync(stream, ct);

        var command = new AnexarLogoObraCommand(id, stream.ToArray(), body.Logo.ContentType);
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "organizacional:ver")]
    [HttpGet("{id:guid}/logo")]
    public async Task<IActionResult> ObterLogo(Guid id, CancellationToken ct)
    {
        var logo = await _mediator.Send(new ObterLogoObraQuery(id), ct);
        return logo is null ? NotFound() : File(logo.Conteudo, logo.ContentType, logo.NomeArquivo);
    }
}

public class AnexarLogoObraRequestBody
{
    public IFormFile Logo { get; set; } = null!;
}

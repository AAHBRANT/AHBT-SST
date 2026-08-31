using AAHBRANT.SST.Application.Obras.Commands;
using AAHBRANT.SST.Application.Obras.Queries;
using AAHBRANT.SST.Domain.Enums;
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

    // Logomarca obrigatória no cadastro (decisão do usuário, 31/08) — o corpo passa a ser
    // multipart/form-data (como o endpoint de anexar logo) em vez de JSON, para poder exigir o
    // arquivo já na criação da obra em vez de um segundo passo opcional.
    [Authorize(Policy = "organizacional:criar")]
    [HttpPost]
    [RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> Criar([FromForm] CriarObraRequestBody body, CancellationToken ct)
    {
        await using var stream = new MemoryStream();
        await body.Logo.CopyToAsync(stream, ct);

        var command = new CriarObraCommand(
            body.Codigo,
            body.Nome,
            body.Cliente,
            body.Status,
            body.DataInicio,
            body.DataPrevisaoTermino,
            body.Endereco,
            body.Cidade,
            body.Uf,
            body.Cnpj,
            stream.ToArray(),
            body.Logo.ContentType);

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

public class CriarObraRequestBody
{
    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string? Cliente { get; set; }
    public StatusObra Status { get; set; }
    public DateTime? DataInicio { get; set; }
    public DateTime? DataPrevisaoTermino { get; set; }
    public string? Endereco { get; set; }
    public string? Cidade { get; set; }
    public string? Uf { get; set; }
    public string? Cnpj { get; set; }
    public IFormFile Logo { get; set; } = null!;
}

using AAHBRANT.SST.Application.Empresas.Commands;
using AAHBRANT.SST.Application.Empresas.Queries;
using AAHBRANT.SST.Application.Terceirizados.Conformidade;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmpresasController : ControllerBase
{
    private readonly IMediator _mediator;
    public EmpresasController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "terceirizado:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
        => Ok(await _mediator.Send(new ListarEmpresasQuery(), ct));

    [Authorize(Policy = "terceirizado:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var empresa = await _mediator.Send(new ObterEmpresaPorIdQuery(id), ct);
        return empresa is null ? NotFound() : Ok(empresa);
    }

    [Authorize(Policy = "terceirizado:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarEmpresaCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }

    [Authorize(Policy = "terceirizado:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarEmpresaCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "terceirizado:excluir")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirEmpresaCommand(id), ct);
        return NoContent();
    }

    // ---- Checklist e percentual de adequação (especificação 21/10/2026, etapa 7) ----

    [Authorize(Policy = "terceirizado:ver")]
    [HttpGet("conformidade")]
    public async Task<IActionResult> ListarConformidade(CancellationToken ct)
        => Ok(await _mediator.Send(new ListarConformidadeEmpresasQuery(), ct));

    [Authorize(Policy = "terceirizado:ver")]
    [HttpGet("{id:guid}/conformidade")]
    public async Task<IActionResult> ObterConformidade(Guid id, CancellationToken ct)
    {
        var conformidade = await _mediator.Send(new ObterConformidadeEmpresaQuery(id), ct);
        return conformidade is null ? NotFound() : Ok(conformidade);
    }

    [Authorize(Policy = "terceirizado:ver")]
    [HttpGet("documentos-exigidos")]
    public async Task<IActionResult> ListarDocumentosExigidos(CancellationToken ct)
        => Ok(await _mediator.Send(new ListarDocumentosExigidosEmpresaQuery(), ct));

    [Authorize(Policy = "terceirizado:editar")]
    [HttpPost("documentos-exigidos")]
    public async Task<IActionResult> CriarDocumentoExigido(SalvarDocumentoExigidoEmpresaCommand command, CancellationToken ct)
        => Ok(new { id = await _mediator.Send(command with { Id = null }, ct) });

    [Authorize(Policy = "terceirizado:editar")]
    [HttpPut("documentos-exigidos/{documentoId:guid}")]
    public async Task<IActionResult> AtualizarDocumentoExigido(Guid documentoId, SalvarDocumentoExigidoEmpresaCommand command, CancellationToken ct)
    {
        await _mediator.Send(command with { Id = documentoId }, ct);
        return NoContent();
    }

    [Authorize(Policy = "terceirizado:editar")]
    [HttpPost("{id:guid}/documentos")]
    [RequestSizeLimit(21_000_000)]
    public async Task<IActionResult> EnviarDocumento(Guid id, [FromForm] EnviarDocumentoEmpresaRequest body, CancellationToken ct)
    {
        using var memoria = new MemoryStream();
        await body.Arquivo.CopyToAsync(memoria, ct);
        var documentoId = await _mediator.Send(new EnviarDocumentoEmpresaCommand(
            id, body.DocumentoExigidoId, body.DataValidade, body.Arquivo.FileName, body.Arquivo.ContentType, memoria.ToArray()), ct);
        return Ok(new { id = documentoId });
    }

    [Authorize(Policy = "terceirizado:ver")]
    [HttpGet("{id:guid}/documentos/{documentoId:guid}/arquivo")]
    public async Task<IActionResult> ObterArquivoDocumento(Guid id, Guid documentoId, CancellationToken ct)
    {
        var arquivo = await _mediator.Send(new ObterArquivoDocumentoEmpresaQuery(id, documentoId), ct);
        return arquivo is null ? NotFound() : File(arquivo.Conteudo, arquivo.ContentType, arquivo.NomeArquivo);
    }

    [Authorize(Policy = "terceirizado:editar")]
    [HttpDelete("{id:guid}/documentos/{documentoId:guid}")]
    public async Task<IActionResult> RemoverDocumento(Guid id, Guid documentoId, CancellationToken ct)
    {
        await _mediator.Send(new RemoverDocumentoEmpresaCommand(id, documentoId), ct);
        return NoContent();
    }
}

public class EnviarDocumentoEmpresaRequest
{
    public Guid DocumentoExigidoId { get; set; }
    public DateOnly? DataValidade { get; set; }
    public IFormFile Arquivo { get; set; } = null!;
}

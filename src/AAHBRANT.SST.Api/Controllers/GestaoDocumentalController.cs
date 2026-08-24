using AAHBRANT.SST.Application.GestaoDocumental.Commands;
using AAHBRANT.SST.Application.GestaoDocumental.Queries;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GestaoDocumentalController : ControllerBase
{
    private readonly IMediator _mediator;

    public GestaoDocumentalController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "documento:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string? nome,
        [FromQuery] string? tipo,
        [FromQuery] string? categoria,
        [FromQuery] StatusDocumentoGestao? status,
        [FromQuery] Guid? obraId,
        [FromQuery] Guid? setorId,
        CancellationToken ct)
        => Ok(await _mediator.Send(new ListarDocumentosGestaoQuery(nome, tipo, categoria, status, obraId, setorId), ct));

    [Authorize(Policy = "documento:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterDetalhe(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new ObterDocumentoGestaoDetalheQuery(id), ct));

    [Authorize(Policy = "documento:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarDocumentoGestaoCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterDetalhe), new { id }, new { id });
    }

    [Authorize(Policy = "documento:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarDocumentoGestaoRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new AtualizarDocumentoGestaoCommand(
            id, body.Nome, body.Tipo, body.Categoria, body.OrigemDocumento, body.ResponsavelUsuarioId,
            body.Versao, body.Validade, body.DataEmissao, body.DataRevisao, body.RequisitoLegalId,
            body.ObraId, body.SetorId, body.Arquivo), ct);
        return NoContent();
    }

    [Authorize(Policy = "documento:editar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirDocumentoGestaoCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = "documento:atualizar_status")]
    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> AtualizarStatus(Guid id, AtualizarStatusDocumentoGestaoRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new AtualizarStatusDocumentoGestaoCommand(id, body.NovoStatus), ct);
        return NoContent();
    }

    [Authorize(Policy = "documento:revisar")]
    [HttpPost("{id:guid}/revisoes")]
    public async Task<IActionResult> CriarRevisao(Guid id, CriarRevisaoDocumentoRequestBody body, CancellationToken ct)
    {
        var revisaoId = await _mediator.Send(
            new CriarRevisaoDocumentoCommand(id, body.Motivo, body.ResponsavelUsuarioId, body.NovaVersao), ct);
        return CreatedAtAction(nameof(ObterDetalhe), new { id }, new { id = revisaoId });
    }
}

public record AtualizarDocumentoGestaoRequestBody(
    string Nome,
    string? Tipo,
    string? Categoria,
    string? OrigemDocumento,
    Guid? ResponsavelUsuarioId,
    string? Versao,
    DateTime? Validade,
    DateTime DataEmissao,
    DateTime? DataRevisao,
    Guid? RequisitoLegalId,
    Guid? ObraId,
    Guid? SetorId,
    string? Arquivo);

public record AtualizarStatusDocumentoGestaoRequestBody(StatusDocumentoGestao NovoStatus);

public record CriarRevisaoDocumentoRequestBody(string Motivo, Guid? ResponsavelUsuarioId, string? NovaVersao);

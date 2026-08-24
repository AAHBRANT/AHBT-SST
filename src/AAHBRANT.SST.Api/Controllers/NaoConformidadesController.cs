using AAHBRANT.SST.Application.NaoConformidades.Commands;
using AAHBRANT.SST.Application.NaoConformidades.Queries;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NaoConformidadesController : ControllerBase
{
    private readonly IMediator _mediator;

    public NaoConformidadesController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "nc:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] StatusNaoConformidade? status, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarNaoConformidadesQuery(status), ct));

    [Authorize(Policy = "nc:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterDetalhe(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new ObterNaoConformidadeDetalheQuery(id), ct));

    [Authorize(Policy = "nc:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarNaoConformidadeCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterDetalhe), new { id }, new { id });
    }

    [Authorize(Policy = "nc:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarNaoConformidadeRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new AtualizarNaoConformidadeCommand(
            id, body.OrigemDeteccao, body.RequisitoRelacionado, body.Descricao, body.Local,
            body.AtividadeId, body.RiscoId, body.ResponsavelUsuarioId, body.Prazo), ct);
        return NoContent();
    }

    [Authorize(Policy = "nc:editar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirNaoConformidadeCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = "nc:avancar_status")]
    [HttpPost("{id:guid}/avancar-status")]
    public async Task<IActionResult> AvancarStatus(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new AvancarStatusNaoConformidadeCommand(id), ct);
        return NoContent();
    }
}

public record AtualizarNaoConformidadeRequestBody(
    OrigemNaoConformidade OrigemDeteccao,
    string? RequisitoRelacionado,
    string Descricao,
    string? Local,
    Guid? AtividadeId,
    Guid? RiscoId,
    Guid? ResponsavelUsuarioId,
    DateTime? Prazo);

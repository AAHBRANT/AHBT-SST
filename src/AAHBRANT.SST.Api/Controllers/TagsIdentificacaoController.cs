using AAHBRANT.SST.Application.TagsIdentificacao.Commands;
using AAHBRANT.SST.Application.TagsIdentificacao.Queries;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TagsIdentificacaoController : ControllerBase
{
    private readonly IMediator _mediator;

    public TagsIdentificacaoController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "identificacao:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] StatusTag? status, [FromQuery] TipoTag? tipo, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarTagsIdentificacaoQuery(status, tipo), ct));

    [Authorize(Policy = "identificacao:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var tag = await _mediator.Send(new ObterTagIdentificacaoPorIdQuery(id), ct);
        return tag is null ? NotFound() : Ok(tag);
    }

    // NTAG.md §1 — ponto de entrada da leitura de NFC/QR: dado o Uid lido pelo app/dispositivo,
    // resolve para a entidade de SST correspondente (Área/Trabalhador) hoje cadastrada.
    [Authorize(Policy = "identificacao:ver")]
    [HttpGet("resolver/{uid}")]
    public async Task<IActionResult> ResolverPorUid(string uid, CancellationToken ct)
    {
        var resolvido = await _mediator.Send(new ResolverTagPorUidQuery(uid), ct);
        return resolvido is null ? NotFound() : Ok(resolvido);
    }

    [Authorize(Policy = "identificacao:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarTagIdentificacaoCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }

    [Authorize(Policy = "identificacao:editar")]
    [HttpPost("{id:guid}/vincular")]
    public async Task<IActionResult> Vincular(Guid id, VincularTagRequest request, CancellationToken ct)
    {
        await _mediator.Send(new VincularTagCommand(id, request.EntidadeVinculadaTipo, request.EntidadeVinculadaId), ct);
        return NoContent();
    }

    // Fluxo de campo: vincula direto pelo Uid lido na tag, sem precisar do Id (Guid) interno.
    [Authorize(Policy = "identificacao:editar")]
    [HttpPost("vincular-por-uid")]
    public async Task<IActionResult> VincularPorUid(VincularTagPorUidRequest request, CancellationToken ct)
    {
        await _mediator.Send(new VincularTagPorUidCommand(request.Uid, request.EntidadeVinculadaTipo, request.EntidadeVinculadaId), ct);
        return NoContent();
    }

    [Authorize(Policy = "identificacao:editar")]
    [HttpPost("{id:guid}/desvincular")]
    public async Task<IActionResult> Desvincular(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DesvincularTagCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = "identificacao:editar")]
    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> AtualizarStatus(Guid id, AtualizarStatusTagRequest request, CancellationToken ct)
    {
        await _mediator.Send(new AtualizarStatusTagCommand(id, request.Status), ct);
        return NoContent();
    }

    [Authorize(Policy = "identificacao:editar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirTagIdentificacaoCommand(id), ct);
        return NoContent();
    }
}

public record VincularTagRequest(TipoEntidadeVinculada EntidadeVinculadaTipo, Guid EntidadeVinculadaId);
public record VincularTagPorUidRequest(string Uid, TipoEntidadeVinculada EntidadeVinculadaTipo, Guid EntidadeVinculadaId);
public record AtualizarStatusTagRequest(StatusTag Status);

using AAHBRANT.SST.Application.PermissaoTrabalhoEpcs.Commands;
using AAHBRANT.SST.Application.PermissaoTrabalhoEpis.Commands;
using AAHBRANT.SST.Application.PermissaoTrabalhoPreRequisitos.Commands;
using AAHBRANT.SST.Application.PermissaoTrabalhoRiscosCriticos.Commands;
using AAHBRANT.SST.Application.PermissaoTrabalhoTiposTrabalho.Commands;
using AAHBRANT.SST.Application.PermissaoTrabalhoVerificacoes.Commands;
using AAHBRANT.SST.Application.PermissoesTrabalho.Commands;
using AAHBRANT.SST.Application.PermissoesTrabalho.Queries;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PermissoesTrabalhoController : ControllerBase
{
    private readonly IMediator _mediator;

    public PermissoesTrabalhoController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "pt:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? atividadeId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarPermissoesTrabalhoQuery(atividadeId), ct));

    [Authorize(Policy = "pt:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterDetalhe(Guid id, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterPermissaoTrabalhoDetalheQuery(id), ct);
        return detalhe is null ? NotFound() : Ok(detalhe);
    }

    [Authorize(Policy = "pt:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarPermissaoTrabalhoCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterDetalhe), new { id }, new { id });
    }

    [Authorize(Policy = "pt:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarPermissaoTrabalhoCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "pt:editar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirPermissaoTrabalhoCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = "pt:autorizar")]
    [HttpPost("{id:guid}/autorizar")]
    public async Task<IActionResult> Autorizar(Guid id, AutorizarPermissaoTrabalhoRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new AutorizarPermissaoTrabalhoCommand(id, body.AutorizadoPorUsuarioId, body.ResponsavelSstUsuarioId), ct);
        return NoContent();
    }

    [Authorize(Policy = "pt:autorizar")]
    [HttpPost("{id:guid}/suspender")]
    public async Task<IActionResult> Suspender(Guid id, SuspenderPermissaoTrabalhoRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new SuspenderPermissaoTrabalhoCommand(id, body.Motivo, body.SuspensaPorUsuarioId), ct);
        return NoContent();
    }

    [Authorize(Policy = "pt:autorizar")]
    [HttpPost("{id:guid}/revalidar")]
    public async Task<IActionResult> Revalidar(Guid id, RevalidarPermissaoTrabalhoRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new RevalidarPermissaoTrabalhoCommand(
            id, body.NovaValidade, body.NovoHorarioFim, body.RevalidadaPorUsuarioId), ct);
        return NoContent();
    }

    [Authorize(Policy = "pt:encerrar")]
    [HttpPost("{id:guid}/encerrar")]
    public async Task<IActionResult> Encerrar(Guid id, EncerrarPermissaoTrabalhoRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new EncerrarPermissaoTrabalhoCommand(id, body.EncerradaPorUsuarioId, body.Observacoes), ct);
        return NoContent();
    }

    [Authorize(Policy = "pt:editar")]
    [HttpPost("{id:guid}/pre-requisitos/{itemId:guid}/marcar")]
    public async Task<IActionResult> MarcarPreRequisito(Guid itemId, MarcarPreRequisitoRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new MarcarPermissaoTrabalhoPreRequisitoCommand(itemId, body.Atendido), ct);
        return NoContent();
    }

    [Authorize(Policy = "pt:editar")]
    [HttpPost("{id:guid}/verificacoes/{itemId:guid}/responder")]
    public async Task<IActionResult> ResponderVerificacao(Guid itemId, ResponderVerificacaoRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new ResponderPermissaoTrabalhoVerificacaoCommand(itemId, body.Resposta), ct);
        return NoContent();
    }

    [Authorize(Policy = "pt:editar")]
    [HttpPut("{id:guid}/tipos-trabalho")]
    public async Task<IActionResult> DefinirTiposTrabalho(Guid id, DefinirTiposTrabalhoRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new DefinirTiposTrabalhoPtCommand(id, body.Tipos), ct);
        return NoContent();
    }

    [Authorize(Policy = "pt:editar")]
    [HttpPut("{id:guid}/epis")]
    public async Task<IActionResult> DefinirEpis(Guid id, DefinirEpisRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new DefinirEpisPtCommand(id, body.Itens, body.OutrosEpis), ct);
        return NoContent();
    }

    [Authorize(Policy = "pt:editar")]
    [HttpPut("{id:guid}/epcs")]
    public async Task<IActionResult> DefinirEpcs(Guid id, DefinirEpcsRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new DefinirEpcsPtCommand(id, body.Itens, body.OutrosEpcs), ct);
        return NoContent();
    }

    [Authorize(Policy = "pt:editar")]
    [HttpPost("riscos-criticos")]
    public async Task<IActionResult> CriarRiscoCritico(CriarPermissaoTrabalhoRiscoCriticoCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return Ok(new { id });
    }

    [Authorize(Policy = "pt:editar")]
    [HttpPut("riscos-criticos/{riscoId:guid}")]
    public async Task<IActionResult> AtualizarRiscoCritico(Guid riscoId, AtualizarRiscoCriticoRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new AtualizarPermissaoTrabalhoRiscoCriticoCommand(
            riscoId, body.RiscoCondicao, body.ControleComplementar, body.ResponsavelEvidencia), ct);
        return NoContent();
    }

    [Authorize(Policy = "pt:editar")]
    [HttpDelete("riscos-criticos/{riscoId:guid}")]
    public async Task<IActionResult> ExcluirRiscoCritico(Guid riscoId, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirPermissaoTrabalhoRiscoCriticoCommand(riscoId), ct);
        return NoContent();
    }

    [Authorize(Policy = "pt:ver")]
    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> ExportarPdf(Guid id, CancellationToken ct)
    {
        var pdf = await _mediator.Send(new ExportarPermissaoTrabalhoPdfQuery(id), ct);
        if (pdf is null) return NotFound();
        return File(pdf, "application/pdf", $"pt-{id}.pdf");
    }
}

public record AutorizarPermissaoTrabalhoRequestBody(Guid AutorizadoPorUsuarioId, Guid? ResponsavelSstUsuarioId);
public record SuspenderPermissaoTrabalhoRequestBody(string Motivo, Guid SuspensaPorUsuarioId);
public record RevalidarPermissaoTrabalhoRequestBody(DateTime NovaValidade, TimeSpan? NovoHorarioFim, Guid RevalidadaPorUsuarioId);
public record EncerrarPermissaoTrabalhoRequestBody(Guid EncerradaPorUsuarioId, string? Observacoes);
public record MarcarPreRequisitoRequestBody(bool Atendido);
public record ResponderVerificacaoRequestBody(RespostaVerificacaoPt Resposta);
public record DefinirTiposTrabalhoRequestBody(List<TipoTrabalhoInput> Tipos);
public record DefinirEpisRequestBody(List<EpiInput> Itens, string? OutrosEpis);
public record DefinirEpcsRequestBody(List<ItemEpcPt> Itens, string? OutrosEpcs);
public record AtualizarRiscoCriticoRequestBody(string RiscoCondicao, string? ControleComplementar, string? ResponsavelEvidencia);

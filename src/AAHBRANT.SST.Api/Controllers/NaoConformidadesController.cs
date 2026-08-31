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

    [Authorize(Policy = "nc:enviar")]
    [HttpPost("{id:guid}/enviar")]
    public async Task<IActionResult> Enviar(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new EnviarNaoConformidadeCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = "nc:responder")]
    [HttpPost("{id:guid}/responder")]
    public async Task<IActionResult> Responder(Guid id, ResponderNaoConformidadeRequestBody body, CancellationToken ct)
    {
        var acaoId = await _mediator.Send(new ResponderNaoConformidadeCommand(
            id, body.DescricaoAcao, body.ResponsavelExecucaoId, body.Prioridade, body.Prazo, body.JustificativaPrazo), ct);
        return Ok(new { acaoId });
    }

    [Authorize(Policy = "nc:responder")]
    [HttpPost("{id:guid}/registrar-conclusao")]
    public async Task<IActionResult> RegistrarConclusao(Guid id, RegistrarConclusaoNaoConformidadeRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new RegistrarConclusaoNaoConformidadeCommand(id, body.DescricaoConclusao), ct);
        return NoContent();
    }

    [Authorize(Policy = "nc:encerrar")]
    [HttpPost("{id:guid}/devolver")]
    public async Task<IActionResult> Devolver(Guid id, DevolverNaoConformidadeRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new DevolverNaoConformidadeCommand(id, body.Motivo), ct);
        return NoContent();
    }

    [Authorize(Policy = "nc:encerrar")]
    [HttpPost("{id:guid}/encerrar")]
    public async Task<IActionResult> Encerrar(Guid id, EncerrarNaoConformidadeRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new EncerrarNaoConformidadeCommand(id, body.ValidadoPorUsuarioId, body.ObservacoesEncerramento), ct);
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

public record ResponderNaoConformidadeRequestBody(
    string DescricaoAcao,
    Guid? ResponsavelExecucaoId,
    PrioridadeAcao Prioridade,
    DateTime? Prazo,
    string? JustificativaPrazo);

public record RegistrarConclusaoNaoConformidadeRequestBody(string? DescricaoConclusao);

public record DevolverNaoConformidadeRequestBody(string Motivo);

public record EncerrarNaoConformidadeRequestBody(Guid ValidadoPorUsuarioId, string? ObservacoesEncerramento);

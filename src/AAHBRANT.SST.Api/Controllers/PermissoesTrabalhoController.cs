using AAHBRANT.SST.Application.PermissoesTrabalho.Commands;
using AAHBRANT.SST.Application.PermissoesTrabalho.Queries;
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
        await _mediator.Send(new AutorizarPermissaoTrabalhoCommand(id, body.AutorizadoPorUsuarioId), ct);
        return NoContent();
    }

    [Authorize(Policy = "pt:encerrar")]
    [HttpPost("{id:guid}/encerrar")]
    public async Task<IActionResult> Encerrar(Guid id, EncerrarPermissaoTrabalhoRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new EncerrarPermissaoTrabalhoCommand(id, body.EncerradaPorUsuarioId, body.Observacoes), ct);
        return NoContent();
    }
}

public record AutorizarPermissaoTrabalhoRequestBody(Guid AutorizadoPorUsuarioId);
public record EncerrarPermissaoTrabalhoRequestBody(Guid EncerradaPorUsuarioId, string? Observacoes);

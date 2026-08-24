using AAHBRANT.SST.Application.AcoesPlano.Commands;
using AAHBRANT.SST.Application.AcoesPlano.Queries;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

// Rota "api/acoesplano" — módulo genérico e reutilizável (NC, e futuramente Acidentes/Auditorias),
// deliberadamente distinto de "api/planoacao" (PlanoAcaoItem, específico do PGR, já existente).
[ApiController]
[Route("api/[controller]")]
public class AcoesPlanoController : ControllerBase
{
    private readonly IMediator _mediator;

    public AcoesPlanoController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "planoacao:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] string origemTipo, [FromQuery] Guid origemId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarAcoesPlanoQuery(origemTipo, origemId), ct));

    [Authorize(Policy = "planoacao:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarAcaoPlanoCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(Listar), new { origemTipo = command.OrigemTipo, origemId = command.OrigemId }, new { id });
    }

    [Authorize(Policy = "planoacao:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarAcaoPlanoRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new AtualizarAcaoPlanoCommand(
            id, body.Tipo, body.Descricao, body.ResponsavelUsuarioId, body.Prioridade,
            body.Prazo, body.Status, body.DataConclusao), ct);
        return NoContent();
    }

    [Authorize(Policy = "planoacao:editar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirAcaoPlanoCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = "planoacao:validar")]
    [HttpPost("{id:guid}/validar")]
    public async Task<IActionResult> Validar(Guid id, ValidarAcaoPlanoRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new ValidarAcaoPlanoCommand(id, body.ValidadoPorUsuarioId), ct);
        return NoContent();
    }
}

public record AtualizarAcaoPlanoRequestBody(
    TipoAcaoPlano Tipo,
    string Descricao,
    Guid? ResponsavelUsuarioId,
    PrioridadeAcao Prioridade,
    DateTime? Prazo,
    StatusControleRisco Status,
    DateTime? DataConclusao);

public record ValidarAcaoPlanoRequestBody(Guid ValidadoPorUsuarioId);

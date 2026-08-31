using AAHBRANT.SST.Application.QuestionarioAplicabilidade.Commands;
using AAHBRANT.SST.Application.QuestionarioAplicabilidade.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/questionario-aplicabilidade")]
public class QuestionarioAplicabilidadeController : ControllerBase
{
    private readonly IMediator _mediator;

    public QuestionarioAplicabilidadeController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "requisitolegal:ver")]
    [HttpGet("itens")]
    public async Task<IActionResult> ListarItens(CancellationToken ct)
        => Ok(await _mediator.Send(new ListarItensQuestionarioAplicabilidadeQuery(), ct));

    [Authorize(Policy = "questionarioaplicabilidade:gerenciar")]
    [HttpPost("itens")]
    public async Task<IActionResult> CriarItem(CriarItemQuestionarioAplicabilidadeCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return Ok(new { id });
    }

    [Authorize(Policy = "questionarioaplicabilidade:gerenciar")]
    [HttpPut("itens/{id:guid}")]
    public async Task<IActionResult> AtualizarItem(Guid id, AtualizarItemQuestionarioAplicabilidadeRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new AtualizarItemQuestionarioAplicabilidadeCommand(id, body.Pergunta, body.TextoApoio), ct);
        return NoContent();
    }

    [Authorize(Policy = "questionarioaplicabilidade:gerenciar")]
    [HttpDelete("itens/{id:guid}")]
    public async Task<IActionResult> ExcluirItem(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirItemQuestionarioAplicabilidadeCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = "questionarioaplicabilidade:responder")]
    [HttpGet("obras/{obraId:guid}")]
    public async Task<IActionResult> ObterQuestionarioObra(Guid obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ObterQuestionarioAplicabilidadeObraQuery(obraId), ct));

    [Authorize(Policy = "questionarioaplicabilidade:responder")]
    [HttpPut("obras/{obraId:guid}/itens/{itemId:guid}")]
    public async Task<IActionResult> Responder(Guid obraId, Guid itemId, ResponderQuestionarioRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new ResponderQuestionarioAplicabilidadeCommand(obraId, itemId, body.Resposta, body.Observacao), ct);
        return NoContent();
    }
}

public record AtualizarItemQuestionarioAplicabilidadeRequestBody(string Pergunta, string? TextoApoio);
public record ResponderQuestionarioRequestBody(bool Resposta, string? Observacao);

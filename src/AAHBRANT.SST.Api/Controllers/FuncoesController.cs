using AAHBRANT.SST.Application.Funcoes.Commands;
using AAHBRANT.SST.Application.Funcoes.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FuncoesController : ControllerBase
{
    private readonly IMediator _mediator;

    public FuncoesController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "organizacional:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
        => Ok(await _mediator.Send(new ListarFuncoesQuery(), ct));

    [Authorize(Policy = "organizacional:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var funcao = await _mediator.Send(new ObterFuncaoPorIdQuery(id), ct);
        return funcao is null ? NotFound() : Ok(funcao);
    }

    [Authorize(Policy = "organizacional:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarFuncaoCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }

    [Authorize(Policy = "organizacional:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarFuncaoCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "organizacional:excluir")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirFuncaoCommand(id), ct);
        return NoContent();
    }
}

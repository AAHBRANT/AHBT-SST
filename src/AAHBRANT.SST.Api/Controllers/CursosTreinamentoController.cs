using AAHBRANT.SST.Application.CursosTreinamento.Commands;
using AAHBRANT.SST.Application.CursosTreinamento.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CursosTreinamentoController : ControllerBase
{
    private readonly IMediator _mediator;

    public CursosTreinamentoController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "treinamento:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
        => Ok(await _mediator.Send(new ListarCursosTreinamentoQuery(), ct));

    // Card "Aptidão por treinamento" do Início: por curso exigido na matriz de funções, quantos
    // trabalhadores ativos estão aptos, a vencer, vencidos ou sem o curso. obraId filtra pela obra.
    [Authorize(Policy = "treinamento:ver")]
    [HttpGet("aptidao")]
    public async Task<IActionResult> ObterAptidao([FromQuery] Guid? obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ObterAptidaoPorCursoQuery(obraId), ct));

    // Cursos cuja norma cita a NR-06 mas não habilitam a entrega de EPI no servidor (ver a query).
    [Authorize(Policy = "treinamento:ver")]
    [HttpGet("nr06-sem-marcador")]
    public async Task<IActionResult> ListarNr06SemMarcador(CancellationToken ct)
        => Ok(await _mediator.Send(new ListarCursosNr6SemMarcadorQuery(), ct));

    [Authorize(Policy = "treinamento:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var curso = await _mediator.Send(new ObterCursoTreinamentoPorIdQuery(id), ct);
        return curso is null ? NotFound() : Ok(curso);
    }

    [Authorize(Policy = "treinamento:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarCursoTreinamentoCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }

    [Authorize(Policy = "treinamento:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarCursoTreinamentoCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "treinamento:editar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirCursoTreinamentoCommand(id), ct);
        return NoContent();
    }
}

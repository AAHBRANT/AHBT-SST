using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Application.RequisitosLegais.Commands;
using AAHBRANT.SST.Application.RequisitosLegais.Queries;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RequisitosLegaisController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUsuarioAtualResolver _usuarioAtual;

    public RequisitosLegaisController(IMediator mediator, IUsuarioAtualResolver usuarioAtual)
    {
        _mediator = mediator;
        _usuarioAtual = usuarioAtual;
    }

    [Authorize(Policy = "requisitolegal:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] CategoriaRequisitoLegal? categoria, [FromQuery] StatusRequisitoLegal? status, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarRequisitosLegaisQuery(categoria, status), ct));

    [Authorize(Policy = "requisitolegal:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterDetalhe(Guid id, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterRequisitoLegalDetalheQuery(id), ct);
        return detalhe is null ? NotFound() : Ok(detalhe);
    }

    [Authorize(Policy = "requisitolegal:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarRequisitoLegalCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterDetalhe), new { id }, new { id });
    }

    [Authorize(Policy = "requisitolegal:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarRequisitoLegalCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    // QSMS conferiu o texto oficial e ativa o requisito (Em revisão → Ativo), registrando quem validou.
    [Authorize(Policy = "requisitolegal:editar")]
    [HttpPost("{id:guid}/validar")]
    public async Task<IActionResult> Validar(Guid id, CancellationToken ct)
    {
        var usuarioId = await _usuarioAtual.ObterIdAsync(User, ct);
        await _mediator.Send(new ValidarRequisitoLegalCommand(id, usuarioId, User.FindFirst("name")?.Value), ct);
        return NoContent();
    }

    [Authorize(Policy = "requisitolegal:excluir")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirRequisitoLegalCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = "requisitolegal:editar")]
    [HttpPut("{id:guid}/criterios")]
    public async Task<IActionResult> DefinirCriterios(Guid id, DefinirCriteriosRequest request, CancellationToken ct)
    {
        await _mediator.Send(new DefinirCriteriosRequisitoLegalCommand(id, request.Criterios), ct);
        return NoContent();
    }
}

public record DefinirCriteriosRequest(List<CriterioAplicabilidadeInput> Criterios);

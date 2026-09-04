using AAHBRANT.SST.Application.Dds.Commands;
using AAHBRANT.SST.Application.Dds.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

// Catálogo pré-cadastrado de temas de DDS (31/08) — tema livre opcional, somado aos temas
// automáticos das atividades do dia (01/09). Reaproveita as policies do próprio módulo DDS (sem
// RBAC novo) para minimizar escopo.
[ApiController]
[Route("api/[controller]")]
public class CatalogoTemasDdsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CatalogoTemasDdsController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "dds:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
        => Ok(await _mediator.Send(new ListarCatalogoTemaDdsQuery(), ct));

    [Authorize(Policy = "dds:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarCatalogoTemaDdsCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return Ok(new { id });
    }

    [Authorize(Policy = "dds:criar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarCatalogoTemaDdsRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new AtualizarCatalogoTemaDdsCommand(id, body.Nome, body.Descricao), ct);
        return NoContent();
    }

    [Authorize(Policy = "dds:criar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirCatalogoTemaDdsCommand(id), ct);
        return NoContent();
    }
}

public record AtualizarCatalogoTemaDdsRequestBody(string Nome, string? Descricao);

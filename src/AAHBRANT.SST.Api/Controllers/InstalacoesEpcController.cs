using AAHBRANT.SST.Application.InstalacoesEpc.Commands;
using AAHBRANT.SST.Application.InstalacoesEpc.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InstalacoesEpcController : ControllerBase
{
    private readonly IMediator _mediator;

    public InstalacoesEpcController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "epc:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarInstalacoesEpcQuery(obraId), ct));

    [Authorize(Policy = "epc:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var instalacao = await _mediator.Send(new ObterInstalacaoEpcPorIdQuery(id), ct);
        return instalacao is null ? NotFound() : Ok(instalacao);
    }

    [Authorize(Policy = "epc:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarInstalacaoEpcCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }

    [Authorize(Policy = "epc:editar")]
    [HttpPost("{id:guid}/inspecao")]
    public async Task<IActionResult> RegistrarInspecao(Guid id, RegistrarInspecaoEpcRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new RegistrarInspecaoEpcCommand(id, body.DataInspecao, body.Status, body.Observacoes), ct);
        return NoContent();
    }

    [Authorize(Policy = "epc:editar")]
    [HttpPost("{id:guid}/remocao")]
    public async Task<IActionResult> RegistrarRemocao(Guid id, RegistrarRemocaoEpcRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new RegistrarRemocaoEpcCommand(id, body.DataRemocao, body.Observacoes), ct);
        return NoContent();
    }

    [Authorize(Policy = "epc:editar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirInstalacaoEpcCommand(id), ct);
        return NoContent();
    }
}

public class RegistrarInspecaoEpcRequestBody
{
    public DateTime DataInspecao { get; set; }
    public Domain.Enums.StatusInspecaoEpc Status { get; set; }
    public string? Observacoes { get; set; }
}

public class RegistrarRemocaoEpcRequestBody
{
    public DateTime DataRemocao { get; set; }
    public string? Observacoes { get; set; }
}

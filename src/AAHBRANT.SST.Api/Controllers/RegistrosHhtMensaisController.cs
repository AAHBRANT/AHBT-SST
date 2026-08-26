using AAHBRANT.SST.Application.RegistrosHhtMensais.Commands;
using AAHBRANT.SST.Application.RegistrosHhtMensais.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RegistrosHhtMensaisController : ControllerBase
{
    private readonly IMediator _mediator;

    public RegistrosHhtMensaisController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "hht:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? obraId, [FromQuery] int? ano, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarRegistrosHhtMensaisQuery(obraId, ano), ct));

    [Authorize(Policy = "hht:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarRegistroHhtMensalCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(Listar), new { id }, new { id });
    }

    [Authorize(Policy = "hht:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarRegistroHhtMensalRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new AtualizarRegistroHhtMensalCommand(
            id, body.ObraId, body.Ano, body.Mes, body.HorasHomemTrabalhadas), ct);
        return NoContent();
    }

    [Authorize(Policy = "hht:excluir")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirRegistroHhtMensalCommand(id), ct);
        return NoContent();
    }
}

public record AtualizarRegistroHhtMensalRequestBody(Guid ObraId, int Ano, int Mes, int HorasHomemTrabalhadas);

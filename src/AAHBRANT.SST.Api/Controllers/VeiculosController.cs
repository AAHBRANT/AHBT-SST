using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Application.Veiculos.Commands;
using AAHBRANT.SST.Application.Veiculos.Queries;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AAHBRANT.SST.Api.Controllers;

// Cadastro e inspeção de veículos/equipamentos (retro, escavadeira, caminhões). Reaproveita as
// permissões de inspeção (inspecao:ver/criar) em vez de criar um módulo novo no RBAC: quem inspeciona
// é quem cadastra (Técnico). Exclusão só do Administrador.
[ApiController]
[Route("api/[controller]")]
public class VeiculosController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IWebHostEnvironment _environment;

    public VeiculosController(IMediator mediator, IWebHostEnvironment environment)
    {
        _mediator = mediator;
        _environment = environment;
    }

    [Authorize(Policy = "inspecao:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? obraId, [FromQuery] TipoVeiculo? tipo, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarVeiculosQuery(obraId, tipo), ct));

    [Authorize(Policy = "inspecao:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarVeiculoCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(Listar), new { obraId = command.ObraId }, new { id });
    }

    [Authorize(Policy = "inspecao:criar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarVeiculoCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("O id da rota difere do id do corpo.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = PoliticasAutorizacao.SomenteAdministrador)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirVeiculoCommand(id), ct);
        return NoContent();
    }

    // "Obter ou criar": abrir a inspeção de um veículo sempre resolve para a inspeção em andamento.
    // O usuário logado é o responsável (claim "oid"), mesmo padrão de AlojamentosController.
    [Authorize(Policy = "inspecao:criar")]
    [HttpPost("{veiculoId:guid}/inspecao-atual")]
    public async Task<IActionResult> ObterOuCriarInspecaoAtual(Guid veiculoId, CancellationToken ct)
    {
        var azureAdObjectId = User.FindFirst("oid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(azureAdObjectId) && _environment.IsDevelopment())
            azureAdObjectId = "dev-local-user";

        return Ok(await _mediator.Send(new ObterOuCriarInspecaoVeiculoCommand(veiculoId, azureAdObjectId), ct));
    }
}

using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Application.Inspecoes.Temas;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AAHBRANT.SST.Api.Controllers;

// Temas de inspeção criados pelo usuário (ex.: "Andaime"), ao lado dos cards fixos Alojamento e
// Veículos. Cada tema tem o checklist próprio e os itens cadastrados por obra, cada um com o seu
// histórico de inspeções. Reaproveita as permissões de inspeção (inspecao:ver/criar), como os Veículos;
// exclusão de item só do Administrador.
[ApiController]
[Route("api/temas-inspecao")]
public class TemasInspecaoController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IWebHostEnvironment _environment;

    public TemasInspecaoController(IMediator mediator, IWebHostEnvironment environment)
    {
        _mediator = mediator;
        _environment = environment;
    }

    [Authorize(Policy = "inspecao:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
        => Ok(await _mediator.Send(new ListarTemasInspecaoQuery(), ct));

    [Authorize(Policy = "inspecao:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarTemaInspecaoCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(Listar), new { id }, new { id });
    }

    [Authorize(Policy = "inspecao:ver")]
    [HttpGet("{temaId:guid}/itens")]
    public async Task<IActionResult> ListarItens(Guid temaId, [FromQuery] Guid? obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarItensInspecionaveisQuery(temaId, obraId), ct));

    [Authorize(Policy = "inspecao:criar")]
    [HttpPost("{temaId:guid}/itens")]
    public async Task<IActionResult> CriarItem(Guid temaId, CriarItemInspecionavelRequestBody body, CancellationToken ct)
    {
        var id = await _mediator.Send(new CriarItemInspecionavelCommand(temaId, body.ObraId, body.Identificacao, body.Descricao, body.PeriodicidadeInspecaoDias, body.ResponsavelInspecaoUsuarioId), ct);
        return CreatedAtAction(nameof(ListarItens), new { temaId }, new { id });
    }

    [Authorize(Policy = "inspecao:criar")]
    [HttpPut("itens/{itemId:guid}")]
    public async Task<IActionResult> AtualizarItem(Guid itemId, AtualizarItemInspecionavelRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new AtualizarItemInspecionavelCommand(itemId, body.Descricao, body.PeriodicidadeInspecaoDias, body.ResponsavelInspecaoUsuarioId), ct);
        return NoContent();
    }

    [Authorize(Policy = PoliticasAutorizacao.SomenteAdministrador)]
    [HttpDelete("itens/{itemId:guid}")]
    public async Task<IActionResult> ExcluirItem(Guid itemId, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirItemInspecionavelCommand(itemId), ct);
        return NoContent();
    }

    // "Obter ou criar": abrir a inspeção de um item sempre resolve para a inspeção em andamento.
    // O usuário logado é o responsável (claim "oid"), mesmo padrão de VeiculosController.
    [Authorize(Policy = "inspecao:criar")]
    [HttpPost("itens/{itemId:guid}/inspecao-atual")]
    public async Task<IActionResult> ObterOuCriarInspecaoAtual(Guid itemId, CancellationToken ct)
    {
        var azureAdObjectId = User.FindFirst("oid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(azureAdObjectId) && _environment.IsDevelopment())
            azureAdObjectId = "dev-local-user";

        return Ok(await _mediator.Send(new ObterOuCriarInspecaoItemCommand(itemId, azureAdObjectId), ct));
    }
}

public record CriarItemInspecionavelRequestBody(
    Guid ObraId, string Identificacao, string? Descricao, int? PeriodicidadeInspecaoDias = null, Guid? ResponsavelInspecaoUsuarioId = null);

public record AtualizarItemInspecionavelRequestBody(string? Descricao, int? PeriodicidadeInspecaoDias, Guid? ResponsavelInspecaoUsuarioId);

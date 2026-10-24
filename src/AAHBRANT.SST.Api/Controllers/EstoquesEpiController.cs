using System.Security.Claims;
using AAHBRANT.SST.Application.EstoquesEpi.Commands;
using AAHBRANT.SST.Application.EstoquesEpi.Minimo;
using AAHBRANT.SST.Application.EstoquesEpi.Reposicao;
using AAHBRANT.SST.Application.EstoquesEpi.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

// Fase 3 da reformulação do módulo EPI — estoque segmentado por Obra. Reaproveita as permissões
// já existentes do módulo EPI (não há permissão "estoque:*" separada): epi:ver para consulta,
// epi:criar para entrada manual, epi:editar para ajuste (correção de saldo).
[ApiController]
[Route("api/[controller]")]
public class EstoquesEpiController : ControllerBase
{
    private readonly IMediator _mediator;

    public EstoquesEpiController(IMediator mediator) => _mediator = mediator;

    private string? AzureAdObjectId => User.FindFirst("oid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [Authorize(Policy = "epi:ver")]
    [HttpGet("obra/{obraId:guid}")]
    public async Task<IActionResult> ListarPorObra(Guid obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarEstoqueEpiPorObraQuery(obraId), ct));

    [Authorize(Policy = "epi:ver")]
    [HttpGet("obra/{obraId:guid}/epi/{catalogoEpiId:guid}/movimentacoes")]
    public async Task<IActionResult> ListarMovimentacoes(Guid obraId, Guid catalogoEpiId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarMovimentacoesEstoqueEpiQuery(catalogoEpiId, obraId), ct));

    [Authorize(Policy = "epi:criar")]
    [HttpPost("entrada")]
    public async Task<IActionResult> RegistrarEntrada(RegistrarEntradaEstoqueEpiCommand command, CancellationToken ct)
    {
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "epi:editar")]
    [HttpPost("ajuste")]
    public async Task<IActionResult> Ajustar(AjustarEstoqueEpiCommand command, CancellationToken ct)
    {
        await _mediator.Send(command, ct);
        return NoContent();
    }

    // --- Estoque mínimo inteligente e reposição (especificação 21/10/2026, Módulos 2 e 3) ---

    [Authorize(Policy = "epi:ver")]
    [HttpGet("obra/{obraId:guid}/painel")]
    public async Task<IActionResult> Painel(Guid obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ObterPainelEstoqueEpiQuery(obraId), ct));

    [Authorize(Policy = "epi:editar")]
    [HttpPut("obra/{obraId:guid}/epi/{catalogoEpiId:guid}/minimo")]
    public async Task<IActionResult> DefinirMinimo(Guid obraId, Guid catalogoEpiId, DefinirEstoqueMinimoRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new DefinirEstoqueMinimoEpiCommand(obraId, catalogoEpiId, body.EstoqueMinimo, AzureAdObjectId), ct);
        return NoContent();
    }

    [Authorize(Policy = "epi:editar")]
    [HttpPost("obra/{obraId:guid}/minimos/aplicar-sugestoes")]
    public async Task<IActionResult> AplicarSugestoes(Guid obraId, CancellationToken ct)
        => Ok(new { aplicados = await _mediator.Send(new AplicarSugestoesEstoqueMinimoCommand(obraId, AzureAdObjectId), ct) });

    [Authorize(Policy = "epi:editar")]
    [HttpPut("obra/{obraId:guid}/prazo-reposicao")]
    public async Task<IActionResult> DefinirPrazoReposicao(Guid obraId, DefinirPrazoReposicaoRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new DefinirPrazoReposicaoEpiCommand(obraId, body.Dias), ct);
        return NoContent();
    }

    [Authorize(Policy = "epi:editar")]
    [HttpPost("transferencia")]
    public async Task<IActionResult> Transferir(TransferirEstoqueEpiCommand command, CancellationToken ct)
    {
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "epi:ver")]
    [HttpGet("obra/{obraId:guid}/solicitacoes-reposicao")]
    public async Task<IActionResult> ListarSolicitacoesReposicao(Guid obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarSolicitacoesReposicaoEpiQuery(obraId), ct));

    [Authorize(Policy = "epi:criar")]
    [HttpPost("obra/{obraId:guid}/solicitacoes-reposicao")]
    public async Task<IActionResult> CriarSolicitacaoReposicao(Guid obraId, CriarSolicitacaoReposicaoRequestBody body, CancellationToken ct)
    {
        // IP já normalizado por UseForwardedHeaders (Program.cs); o cabeçalho cru é controlado pelo cliente.
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var id = await _mediator.Send(new CriarSolicitacaoReposicaoEpiCommand(obraId, body.Observacao, AzureAdObjectId, ip), ct);
        return Ok(new { id });
    }

    [Authorize(Policy = "epi:ver")]
    [HttpGet("solicitacoes-reposicao/{id:guid}/pdf")]
    public async Task<IActionResult> ExportarSolicitacaoReposicao(Guid id, CancellationToken ct)
    {
        var pdf = await _mediator.Send(new ExportarSolicitacaoReposicaoEpiPdfQuery(id), ct);
        return pdf is null ? NotFound() : File(pdf, "application/pdf", $"solicitacao-reposicao-epi-{id}.pdf");
    }
}

public record DefinirEstoqueMinimoRequestBody(int? EstoqueMinimo);
public record DefinirPrazoReposicaoRequestBody(int Dias);
public record CriarSolicitacaoReposicaoRequestBody(string? Observacao);

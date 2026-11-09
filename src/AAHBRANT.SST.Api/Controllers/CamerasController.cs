using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Application.Cameras.Commands;
using AAHBRANT.SST.Application.Cameras.Queries;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AAHBRANT.SST.Api.Controllers;

// Câmeras de monitoramento de EPI (capacete e colete). Reaproveita as permissões de inspeção
// (inspecao:ver/criar), mesma decisão do VeiculosController. Exclusão só do Administrador.
[ApiController]
[Route("api/[controller]")]
public class CamerasController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IWebHostEnvironment _environment;

    public CamerasController(IMediator mediator, IWebHostEnvironment environment)
    {
        _mediator = mediator;
        _environment = environment;
    }

    [Authorize(Policy = "inspecao:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarCamerasQuery(obraId), ct));

    [Authorize(Policy = "inspecao:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarCameraCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(Listar), new { obraId = command.ObraId }, new { id });
    }

    [Authorize(Policy = "inspecao:criar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarCameraCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("O id da rota difere do id do corpo.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = PoliticasAutorizacao.SomenteAdministrador)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirCameraCommand(id), ct);
        return NoContent();
    }

    // Recebe um quadro JPEG da tela Monitorar. Sempre responde 200 com a situação
    // (ignorado/limpo/ocorrencia/falha) para o navegador seguir monitorando sem tratar erro.
    [Authorize(Policy = "inspecao:criar")]
    [HttpPost("{id:guid}/quadros")]
    [RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> AnalisarQuadro(Guid id, [FromForm] QuadroCameraForm form, CancellationToken ct)
    {
        if (form.Foto is null || form.Foto.Length == 0) return BadRequest("Envie a foto do quadro.");

        using var memoria = new MemoryStream();
        await form.Foto.CopyToAsync(memoria, ct);
        var capturadaEmUtc = (form.CapturadaEm ?? DateTimeOffset.UtcNow).UtcDateTime;

        return Ok(await _mediator.Send(new AnalisarQuadroCameraCommand(id, memoria.ToArray(), capturadaEmUtc), ct));
    }

    [Authorize(Policy = "inspecao:ver")]
    [HttpGet("ocorrencias")]
    public async Task<IActionResult> ListarOcorrencias(
        [FromQuery] Guid? obraId,
        [FromQuery] Guid? cameraId,
        [FromQuery] DateTimeOffset? de,
        [FromQuery] DateTimeOffset? ate,
        [FromQuery] SituacaoOcorrenciaEpi? situacao,
        CancellationToken ct)
        => Ok(await _mediator.Send(new ListarOcorrenciasEpiQuery(obraId, cameraId, de?.UtcDateTime, ate?.UtcDateTime, situacao), ct));

    [Authorize(Policy = "inspecao:ver")]
    [HttpGet("ocorrencias/{id:guid}/foto")]
    public async Task<IActionResult> ObterFoto(Guid id, CancellationToken ct)
        => File(await _mediator.Send(new ObterFotoOcorrenciaEpiQuery(id), ct), "image/jpeg");

    [Authorize(Policy = "inspecao:criar")]
    [HttpPut("ocorrencias/{id:guid}/trabalhador")]
    public async Task<IActionResult> VincularTrabalhador(Guid id, VincularTrabalhadorRequest corpo, CancellationToken ct)
    {
        var azureAdObjectId = User.FindFirst("oid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(azureAdObjectId) && _environment.IsDevelopment())
            azureAdObjectId = "dev-local-user";

        await _mediator.Send(new VincularTrabalhadorOcorrenciaEpiCommand(id, corpo.TrabalhadorId, azureAdObjectId), ct);
        return NoContent();
    }

    [Authorize(Policy = "inspecao:criar")]
    [HttpPut("ocorrencias/{id:guid}/situacao")]
    public async Task<IActionResult> DefinirSituacao(Guid id, DefinirSituacaoRequest corpo, CancellationToken ct)
    {
        await _mediator.Send(new DefinirSituacaoOcorrenciaEpiCommand(id, corpo.Situacao), ct);
        return NoContent();
    }
}

public class QuadroCameraForm
{
    public IFormFile? Foto { get; set; }
    public DateTimeOffset? CapturadaEm { get; set; }
}

public record VincularTrabalhadorRequest(Guid? TrabalhadorId);

public record DefinirSituacaoRequest(SituacaoOcorrenciaEpi Situacao);

using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Application.Terceirizados.Portal;
using AAHBRANT.SST.Application.Terceirizados.Queries;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/terceirizados")]
public class TerceirizadosController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUsuarioAtualResolver _usuarioAtual;

    public TerceirizadosController(IMediator mediator, IUsuarioAtualResolver usuarioAtual)
    {
        _mediator = mediator;
        _usuarioAtual = usuarioAtual;
    }

    [Authorize(Policy = "terceirizado:ver")]
    [HttpGet("pessoas")]
    public async Task<IActionResult> ListarPessoas([FromQuery] Guid? empresaId, [FromQuery] Guid? contratoId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarPessoasTerceirizadasQuery(empresaId, contratoId), ct));

    [Authorize(Policy = "terceirizado:ver")]
    [HttpGet("pessoas/{trabalhadorId:guid}/status")]
    public async Task<IActionResult> ObterStatus(Guid trabalhadorId, CancellationToken ct)
        => Ok(await _mediator.Send(new ObterStatusLiberacaoTrabalhadorQuery(trabalhadorId), ct));

    [Authorize(Policy = "terceirizado:ver")]
    [HttpGet("pendencias")]
    public async Task<IActionResult> ListarPendencias(CancellationToken ct)
        => Ok(await _mediator.Send(new ListarPendenciasTerceirizadoQuery(), ct));

    // Fila de conferência do que a empresa enviou pelo portal de terceiros.
    [Authorize(Policy = "terceirizado:ver")]
    [HttpGet("documentos-enviados")]
    public async Task<IActionResult> ListarDocumentosEnviados(
        [FromQuery] StatusDocumentoTerceiro? status, [FromQuery] Guid? contratoId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarDocumentosTerceiroQuery(status, contratoId), ct));

    // inline=true abre no visualizador do navegador (conferência); sem o parâmetro, baixa.
    [Authorize(Policy = "terceirizado:ver")]
    [HttpGet("documentos-enviados/{id:guid}/arquivo")]
    public async Task<IActionResult> ObterArquivoDocumentoEnviado(Guid id, [FromQuery] bool inline, CancellationToken ct)
    {
        var arquivo = await _mediator.Send(new ObterArquivoDocumentoTerceiroQuery(id), ct);
        if (arquivo is null) return NotFound();
        return inline
            ? File(arquivo.Conteudo, arquivo.ContentType)
            : File(arquivo.Conteudo, arquivo.ContentType, arquivo.NomeArquivo);
    }

    [Authorize(Policy = "terceirizado:editar")]
    [HttpPost("documentos-enviados/{id:guid}/aprovar")]
    public async Task<IActionResult> AprovarDocumentoEnviado(Guid id, AprovarDocumentoEnviadoRequest? request, CancellationToken ct)
    {
        var usuarioId = await _usuarioAtual.ObterIdAsync(User, ct);
        var geradoId = await _mediator.Send(new AprovarDocumentoTerceiroCommand(
            id, usuarioId, request?.DataDocumento, request?.DataValidade, request?.TipoExameAso, request?.ResultadoAso, request?.CargaHoraria), ct);
        return Ok(new { id = geradoId });
    }

    [Authorize(Policy = "terceirizado:editar")]
    [HttpPost("documentos-enviados/{id:guid}/recusar")]
    public async Task<IActionResult> RecusarDocumentoEnviado(Guid id, RecusarDocumentoEnviadoRequest request, CancellationToken ct)
    {
        var usuarioId = await _usuarioAtual.ObterIdAsync(User, ct);
        await _mediator.Send(new RecusarDocumentoTerceiroCommand(id, usuarioId, request.Motivo), ct);
        return NoContent();
    }
}

public record AprovarDocumentoEnviadoRequest(
    DateTime? DataDocumento, DateTime? DataValidade, TipoExameAso? TipoExameAso, ResultadoAso? ResultadoAso, int? CargaHoraria);

public record RecusarDocumentoEnviadoRequest(string Motivo);

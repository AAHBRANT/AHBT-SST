using AAHBRANT.SST.Application.LeituraIa;
using AAHBRANT.SST.Application.Terceirizados.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/terceirizados")]
public class TerceirizadosController : ControllerBase
{
    private readonly IMediator _mediator;
    public TerceirizadosController(IMediator mediator) => _mediator = mediator;

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

    // Importação da pasta da terceirizada (23/10/2026): a tela manda um arquivo por vez e recebe o que ele
    // é (contrato, documento da empresa, ASO, certificado, ficha de EPI) e de quem é. Nada é gravado aqui.
    [Authorize(Policy = "terceirizado:criar")]
    [HttpPost("importacao/classificar")]
    [RequestSizeLimit(21_000_000)]
    public async Task<IActionResult> ClassificarArquivo([FromForm] ClassificarArquivoTerceirizadoRequest body, CancellationToken ct)
    {
        await using var stream = new MemoryStream();
        await body.Arquivo.CopyToAsync(stream, ct);
        return Ok(await _mediator.Send(
            new ClassificarDocumentoTerceirizadoQuery(body.Arquivo.FileName, body.Arquivo.ContentType ?? string.Empty, stream.ToArray()), ct));
    }
}

public class ClassificarArquivoTerceirizadoRequest
{
    public IFormFile Arquivo { get; set; } = null!;
}

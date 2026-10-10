using AAHBRANT.SST.Application.LeituraIa;
using AAHBRANT.SST.Domain.Entidades;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

// "Ler com IA" do PGR e do PCMSO (10/10/2026). Rotas separadas por documento para usar a permissão de
// cada um: ler/cadastrar exige criar PGR (ou PCMSO); consultar exige ver.
[ApiController]
public class LeiturasIaController : ControllerBase
{
    private readonly IMediator _mediator;

    public LeiturasIaController(IMediator mediator) => _mediator = mediator;

    public record CadastrarLeituraBody(List<MapeamentoFuncaoLeituraIa> Funcoes);

    // ---------- PGR ----------

    [Authorize(Policy = "pgr:criar")]
    [HttpPost("api/pgrs/{id:guid}/leituras-ia")]
    public async Task<IActionResult> IniciarPgr(Guid id, CancellationToken ct)
        => Ok(new { id = await _mediator.Send(new IniciarLeituraIaCommand(DocumentoLeituraIa.Pgr, id), ct) });

    [Authorize(Policy = "pgr:ver")]
    [HttpGet("api/pgrs/{id:guid}/leituras-ia/ultima")]
    public async Task<IActionResult> UltimaPgr(Guid id, CancellationToken ct)
    {
        var l = await _mediator.Send(new UltimaLeituraIaQuery(DocumentoLeituraIa.Pgr, id), ct);
        return l is null ? NoContent() : Ok(l);
    }

    [Authorize(Policy = "pgr:ver")]
    [HttpGet("api/pgrs/leituras-ia/{leituraId:guid}")]
    public async Task<IActionResult> ObterPgr(Guid leituraId, CancellationToken ct)
    {
        var l = await _mediator.Send(new ObterLeituraIaQuery(leituraId, DocumentoLeituraIa.Pgr), ct);
        return l is null ? NotFound() : Ok(l);
    }

    [Authorize(Policy = "pgr:criar")]
    [HttpPost("api/pgrs/leituras-ia/{leituraId:guid}/descartar")]
    public async Task<IActionResult> DescartarPgr(Guid leituraId, CancellationToken ct)
    {
        await _mediator.Send(new DescartarLeituraIaCommand(leituraId, DocumentoLeituraIa.Pgr), ct);
        return NoContent();
    }

    [Authorize(Policy = "pgr:criar")]
    [HttpPost("api/pgrs/leituras-ia/{leituraId:guid}/cadastrar")]
    public async Task<IActionResult> CadastrarPgr(Guid leituraId, CadastrarLeituraBody body, CancellationToken ct)
        => Ok(await _mediator.Send(new CadastrarLeituraIaCommand(leituraId, DocumentoLeituraIa.Pgr, body.Funcoes), ct));

    // ---------- PCMSO ----------

    [Authorize(Policy = "pcmso:criar")]
    [HttpPost("api/pcmsos/{id:guid}/leituras-ia")]
    public async Task<IActionResult> IniciarPcmso(Guid id, CancellationToken ct)
        => Ok(new { id = await _mediator.Send(new IniciarLeituraIaCommand(DocumentoLeituraIa.Pcmso, id), ct) });

    [Authorize(Policy = "pcmso:ver")]
    [HttpGet("api/pcmsos/{id:guid}/leituras-ia/ultima")]
    public async Task<IActionResult> UltimaPcmso(Guid id, CancellationToken ct)
    {
        var l = await _mediator.Send(new UltimaLeituraIaQuery(DocumentoLeituraIa.Pcmso, id), ct);
        return l is null ? NoContent() : Ok(l);
    }

    [Authorize(Policy = "pcmso:ver")]
    [HttpGet("api/pcmsos/leituras-ia/{leituraId:guid}")]
    public async Task<IActionResult> ObterPcmso(Guid leituraId, CancellationToken ct)
    {
        var l = await _mediator.Send(new ObterLeituraIaQuery(leituraId, DocumentoLeituraIa.Pcmso), ct);
        return l is null ? NotFound() : Ok(l);
    }

    [Authorize(Policy = "pcmso:criar")]
    [HttpPost("api/pcmsos/leituras-ia/{leituraId:guid}/descartar")]
    public async Task<IActionResult> DescartarPcmso(Guid leituraId, CancellationToken ct)
    {
        await _mediator.Send(new DescartarLeituraIaCommand(leituraId, DocumentoLeituraIa.Pcmso), ct);
        return NoContent();
    }

    [Authorize(Policy = "pcmso:criar")]
    [HttpPost("api/pcmsos/leituras-ia/{leituraId:guid}/cadastrar")]
    public async Task<IActionResult> CadastrarPcmso(Guid leituraId, CadastrarLeituraBody body, CancellationToken ct)
        => Ok(await _mediator.Send(new CadastrarLeituraIaCommand(leituraId, DocumentoLeituraIa.Pcmso, body.Funcoes), ct));
}

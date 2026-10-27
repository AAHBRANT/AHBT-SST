using AAHBRANT.SST.Application.EscopoPcmso;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

// Escopo para o médico elaborar o PCMSO (21/10), montado a partir do PGR vigente da obra.
[ApiController]
[Route("api/escopo-pcmso")]
public class EscopoPcmsoController : ControllerBase
{
    private readonly IMediator _mediator;

    public EscopoPcmsoController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "pcmso:ver")]
    [HttpGet("obras/{obraId:guid}")]
    public async Task<IActionResult> Obter(Guid obraId, CancellationToken ct)
    {
        var escopo = await _mediator.Send(new ObterEscopoPcmsoQuery(obraId), ct);
        return escopo is null ? NotFound() : Ok(escopo);
    }

    [Authorize(Policy = "pcmso:ver")]
    [HttpGet("obras/{obraId:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid obraId, CancellationToken ct)
    {
        var pdf = await _mediator.Send(new ExportarEscopoPcmsoPdfQuery(obraId), ct);
        return pdf is null ? NotFound() : File(pdf, "application/pdf", $"escopo-pcmso-{obraId}.pdf");
    }

    [Authorize(Policy = "pcmso:editar")]
    [HttpPost("obras/{obraId:guid}/enviar")]
    public async Task<IActionResult> Enviar(Guid obraId, EnviarEscopoPcmsoRequest corpo, CancellationToken ct)
    {
        await _mediator.Send(new EnviarEscopoPcmsoCommand(obraId, corpo.EmailMedico, corpo.NomeMedico, corpo.Mensagem,
            User.FindFirst("name")?.Value), ct);
        return NoContent();
    }

    // Funções do PGR vigente contra as funções com exames no PCMSO e os trabalhadores ativos da obra.
    [Authorize(Policy = "pcmso:ver")]
    [HttpGet("obras/{obraId:guid}/comparativo")]
    public async Task<IActionResult> Comparativo(Guid obraId, CancellationToken ct)
    {
        var comparativo = await _mediator.Send(new ObterComparativoPgrPcmsoQuery(obraId), ct);
        return comparativo is null ? NotFound() : Ok(comparativo);
    }

    // Rascunho do PCMSO a partir do PGR vigente: só funções e riscos, sem exames. Recusa se já houver PCMSO ativo.
    [Authorize(Policy = "pcmso:criar")]
    [HttpPost("obras/{obraId:guid}/rascunho")]
    public async Task<IActionResult> CriarRascunho(Guid obraId, CancellationToken ct)
        => Ok(new { id = await _mediator.Send(new CriarRascunhoPcmsoDoPgrCommand(obraId), ct) });
}

public class EnviarEscopoPcmsoRequest
{
    public string EmailMedico { get; set; } = string.Empty;
    public string? NomeMedico { get; set; }
    public string? Mensagem { get; set; }
}

using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.OrdensServico;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Api.Controllers;

// Ordem de Serviço de SST do trabalhador (20/10/2026): minuta e pendências; desde 03/11 também a emissão
// com número, a assinatura (funcionário e depois técnico de segurança, pelo Motor de Assinatura) e o PDF.
[ApiController]
[Route("api/trabalhadores/{trabalhadorId:guid}/ordem-servico")]
public class OrdensServicoController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;

    public OrdensServicoController(IMediator mediator, IAppDbContext db)
    {
        _mediator = mediator;
        _db = db;
    }

    [Authorize(Policy = "trabalhador:ver")]
    [HttpGet("minuta")]
    public async Task<IActionResult> ObterMinuta(Guid trabalhadorId, CancellationToken ct)
    {
        var minuta = await _mediator.Send(new ObterMinutaOrdemServicoQuery(trabalhadorId), ct);
        return minuta is null ? NotFound() : Ok(minuta);
    }

    [Authorize(Policy = "trabalhador:ver")]
    [HttpGet]
    public async Task<ActionResult<List<OrdemServicoEmitidaDto>>> Listar(Guid trabalhadorId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarOrdensServicoQuery(trabalhadorId), ct));

    [Authorize(Policy = "trabalhador:editar")]
    [HttpPost]
    public async Task<ActionResult<OrdemServicoEmitidaDto>> Emitir(Guid trabalhadorId, CancellationToken ct)
        => Ok(await _mediator.Send(new EmitirOrdemServicoCommand(trabalhadorId), ct));

    // Finalizada (as duas assinaturas), devolve o arquivo congelado no Motor; antes disso, gera na hora.
    [Authorize(Policy = "trabalhador:ver")]
    [HttpGet("{ordemServicoId:guid}/pdf")]
    public async Task<IActionResult> BaixarPdf(Guid trabalhadorId, Guid ordemServicoId, CancellationToken ct)
    {
        var os = await _db.OrdensServico
            .Where(o => o.Id == ordemServicoId && o.TrabalhadorId == trabalhadorId)
            .Select(o => new { o.Id, o.Numero })
            .FirstOrDefaultAsync(ct);
        if (os is null) return NotFound();

        var congelado = await _db.DocumentosAssinatura
            .Where(d => d.EntidadeTipo == OrdemServico.EntidadeTipoAssinatura && d.EntidadeId == os.Id
                && d.Status == StatusDocumentoAssinatura.Finalizado)
            .Select(d => d.PdfConteudo)
            .FirstOrDefaultAsync(ct);
        var pdf = congelado ?? await _mediator.Send(new ExportarOrdemServicoPdfQuery(os.Id), ct);
        return pdf is null ? NotFound() : File(pdf, "application/pdf", $"{os.Numero}.pdf");
    }
}

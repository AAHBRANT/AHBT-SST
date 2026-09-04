using AAHBRANT.SST.Application.SessoesTreinamento.Commands;
using AAHBRANT.SST.Application.SessoesTreinamento.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

// Sessão/Turma de Treinamento (04/09) — reaproveita as permissões já existentes do módulo
// Treinamento (não há "treinamento:conduzir"/"treinamento:encerrar" separados): treinamento:ver
// para consulta, treinamento:criar para inscrever/registrar presença/anexar foto, treinamento:editar
// para encerrar (mesmo raciocínio de EstoquesEpiController quanto a reaproveitar policies do módulo).
[ApiController]
[Route("api/[controller]")]
public class SessoesTreinamentoController : ControllerBase
{
    private readonly IMediator _mediator;

    public SessoesTreinamentoController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "treinamento:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarSessoesTreinamentoQuery(obraId), ct));

    [Authorize(Policy = "treinamento:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterDetalhe(Guid id, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterSessaoTreinamentoDetalheQuery(id), ct);
        return detalhe is null ? NotFound() : Ok(detalhe);
    }

    [Authorize(Policy = "treinamento:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarSessaoTreinamentoCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterDetalhe), new { id }, new { id });
    }

    [Authorize(Policy = "treinamento:criar")]
    [HttpPost("{id:guid}/presenca")]
    public async Task<IActionResult> RegistrarPresenca(Guid id, RegistrarPresencaSessaoTreinamentoRequestBody body, CancellationToken ct)
    {
        var command = new RegistrarPresencaSessaoTreinamentoCommand(id, body.TrabalhadorId, body.DispositivoId, body.SegredoDispositivo, body.Score);
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "treinamento:criar")]
    [HttpPost("{id:guid}/fotos-evidencia")]
    [RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> AnexarFotoEvidencia(Guid id, [FromForm] AnexarFotoEvidenciaSessaoTreinamentoRequestBody body, CancellationToken ct)
    {
        await using var stream = new MemoryStream();
        await body.Foto.CopyToAsync(stream, ct);

        var fotoId = await _mediator.Send(new AnexarFotoEvidenciaSessaoTreinamentoCommand(id, body.Ordem, stream.ToArray(), body.Foto.ContentType), ct);
        return Ok(new { id = fotoId });
    }

    [Authorize(Policy = "treinamento:ver")]
    [HttpGet("fotos-evidencia/{fotoId:guid}")]
    public async Task<IActionResult> ObterFotoEvidencia(Guid fotoId, CancellationToken ct)
    {
        var foto = await _mediator.Send(new ObterFotoEvidenciaSessaoTreinamentoQuery(fotoId), ct);
        return foto is null ? NotFound() : File(foto.Conteudo, foto.ContentType, foto.NomeArquivo);
    }

    [Authorize(Policy = "treinamento:criar")]
    [HttpDelete("fotos-evidencia/{fotoId:guid}")]
    public async Task<IActionResult> RemoverFotoEvidencia(Guid fotoId, CancellationToken ct)
    {
        await _mediator.Send(new RemoverFotoEvidenciaSessaoTreinamentoCommand(fotoId), ct);
        return NoContent();
    }

    [Authorize(Policy = "treinamento:editar")]
    [HttpPost("{id:guid}/encerrar")]
    public async Task<IActionResult> Encerrar(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new EncerrarSessaoTreinamentoCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = "treinamento:ver")]
    [HttpGet("{id:guid}/ata/pdf")]
    public async Task<IActionResult> ExportarAta(Guid id, CancellationToken ct)
    {
        var pdf = await _mediator.Send(new ExportarAtaSessaoTreinamentoQuery(id), ct);
        return pdf is null ? NotFound() : File(pdf, "application/pdf", $"ata-turma-treinamento-{id}.pdf");
    }
}

public class RegistrarPresencaSessaoTreinamentoRequestBody
{
    public Guid TrabalhadorId { get; set; }
    public Guid DispositivoId { get; set; }
    public string SegredoDispositivo { get; set; } = string.Empty;
    public double Score { get; set; }
}

public class AnexarFotoEvidenciaSessaoTreinamentoRequestBody
{
    public IFormFile Foto { get; set; } = null!;
    public int Ordem { get; set; }
}

using AAHBRANT.SST.Application.Acidentes.Commands;
using AAHBRANT.SST.Application.Acidentes.Queries;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AcidentesController : ControllerBase
{
    private readonly IMediator _mediator;

    public AcidentesController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "acidente:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] TipoOcorrencia? tipo,
        [FromQuery] StatusAcidente? status,
        [FromQuery] Guid? obraId,
        CancellationToken ct)
        => Ok(await _mediator.Send(new ListarAcidentesQuery(tipo, status, obraId), ct));

    [Authorize(Policy = "acidente:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterDetalhe(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new ObterAcidenteDetalheQuery(id), ct));

    [Authorize(Policy = "acidente:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarAcidenteCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterDetalhe), new { id }, new { id });
    }

    [Authorize(Policy = "acidente:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarAcidenteRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new AtualizarAcidenteCommand(
            id, body.Tipo, body.ObraId, body.TrabalhadorId, body.AtividadeId, body.Local, body.Data,
            body.Hora, body.Descricao, body.Lesao, body.Consequencia, body.Atendimento,
            body.HouveAfastamento, body.DiasAfastamento, body.NumeroCat, body.MetodologiaInvestigacao,
            body.Causas, body.Gravidade, body.DiasDebitadosInformados), ct);
        return NoContent();
    }

    [Authorize(Policy = "acidente:editar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirAcidenteCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = "acidente:avancar_status")]
    [HttpPost("{id:guid}/avancar-status")]
    public async Task<IActionResult> AvancarStatus(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new AvancarStatusAcidenteCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = "acidente:editar")]
    [HttpPost("{id:guid}/fotos")]
    [RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> AnexarFoto(Guid id, [FromForm] AnexarFotoAcidenteBody body, CancellationToken ct)
    {
        await using var stream = new MemoryStream();
        await body.Foto.CopyToAsync(stream, ct);
        var fotoId = await _mediator.Send(new AnexarFotoAcidenteCommand(id, body.Ordem, stream.ToArray(), body.Foto.ContentType, body.Metadados), ct);
        return Ok(new { id = fotoId });
    }

    [Authorize(Policy = "acidente:ver")]
    [HttpGet("fotos/{fotoId:guid}")]
    public async Task<IActionResult> ObterFoto(Guid fotoId, CancellationToken ct)
    {
        var foto = await _mediator.Send(new ObterFotoAcidenteQuery(fotoId), ct);
        return foto is null ? NotFound() : File(foto.Conteudo, foto.ContentType);
    }

    [Authorize(Policy = "acidente:editar")]
    [HttpDelete("fotos/{fotoId:guid}")]
    public async Task<IActionResult> RemoverFoto(Guid fotoId, CancellationToken ct)
    {
        await _mediator.Send(new RemoverFotoAcidenteCommand(fotoId), ct);
        return NoContent();
    }
}

public class AnexarFotoAcidenteBody
{
    public IFormFile Foto { get; set; } = null!;
    public int Ordem { get; set; }
    public string? Metadados { get; set; }
}

public record AtualizarAcidenteRequestBody(
    TipoOcorrencia Tipo,
    Guid ObraId,
    Guid? TrabalhadorId,
    Guid? AtividadeId,
    string Local,
    DateTime Data,
    TimeSpan? Hora,
    string Descricao,
    string? Lesao,
    string? Consequencia,
    string? Atendimento,
    bool HouveAfastamento,
    int? DiasAfastamento,
    string? NumeroCat,
    MetodologiaInvestigacao? MetodologiaInvestigacao,
    string? Causas,
    GravidadeAcidente Gravidade,
    int? DiasDebitadosInformados);

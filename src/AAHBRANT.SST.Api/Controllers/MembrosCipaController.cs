using AAHBRANT.SST.Application.Cipa.Commands;
using AAHBRANT.SST.Application.Cipa.Queries;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MembrosCipaController : ControllerBase
{
    private readonly IMediator _mediator;
    public MembrosCipaController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "cipa:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? obraId, [FromQuery] bool somenteMandatoAtivo, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarMembrosCipaQuery(obraId, somenteMandatoAtivo), ct));

    [Authorize(Policy = "cipa:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterDetalhe(Guid id, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterMembroCipaDetalheQuery(id), ct);
        return detalhe is null ? NotFound() : Ok(detalhe);
    }

    [Authorize(Policy = "cipa:gerenciar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarMembroCipaCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterDetalhe), new { id }, new { id });
    }

    [Authorize(Policy = "cipa:gerenciar")]
    [HttpPut("{id:guid}/cargo")]
    public async Task<IActionResult> DefinirCargo(Guid id, DefinirCargoMembroCipaRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new DefinirCargoMembroCipaCommand(id, body.Cargo), ct);
        return NoContent();
    }

    [Authorize(Policy = "cipa:gerenciar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> EncerrarMandato(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new EncerrarMandatoMembroCipaCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = "cipa:gerenciar")]
    [HttpPost("{id:guid}/treinamentos")]
    public async Task<IActionResult> CriarTreinamento(Guid id, CriarTreinamentoCipaRequestBody body, CancellationToken ct)
    {
        var treinamentoId = await _mediator.Send(new CriarTreinamentoCipaCommand(
            id, body.CargaHoraria, body.ConteudoProgramatico, body.DataRealizacao, body.DataValidade, body.InstituicaoInstrutor), ct);
        return Ok(new { id = treinamentoId });
    }

    [Authorize(Policy = "cipa:gerenciar")]
    [HttpPost("treinamentos/{treinamentoId:guid}/certificado")]
    [RequestSizeLimit(9_000_000)]
    public async Task<IActionResult> AnexarCertificado(Guid treinamentoId, [FromForm] AnexarArquivoTreinamentoCipaRequestBody body, CancellationToken ct)
        => await AnexarArquivo(treinamentoId, TipoArquivoTreinamentoCipa.Certificado, body, ct);

    [Authorize(Policy = "cipa:gerenciar")]
    [HttpPost("treinamentos/{treinamentoId:guid}/lista-presenca")]
    [RequestSizeLimit(9_000_000)]
    public async Task<IActionResult> AnexarListaPresenca(Guid treinamentoId, [FromForm] AnexarArquivoTreinamentoCipaRequestBody body, CancellationToken ct)
        => await AnexarArquivo(treinamentoId, TipoArquivoTreinamentoCipa.ListaPresenca, body, ct);

    private async Task<IActionResult> AnexarArquivo(Guid treinamentoId, TipoArquivoTreinamentoCipa tipo, AnexarArquivoTreinamentoCipaRequestBody body, CancellationToken ct)
    {
        await using var stream = new MemoryStream();
        await body.Arquivo.CopyToAsync(stream, ct);

        await _mediator.Send(new AnexarArquivoTreinamentoCipaCommand(treinamentoId, tipo, stream.ToArray(), body.Arquivo.ContentType), ct);
        return NoContent();
    }

    [Authorize(Policy = "cipa:ver")]
    [HttpGet("treinamentos/{treinamentoId:guid}/certificado")]
    public async Task<IActionResult> ObterCertificado(Guid treinamentoId, CancellationToken ct)
        => await ObterArquivo(treinamentoId, TipoArquivoTreinamentoCipa.Certificado, ct);

    [Authorize(Policy = "cipa:ver")]
    [HttpGet("treinamentos/{treinamentoId:guid}/lista-presenca")]
    public async Task<IActionResult> ObterListaPresenca(Guid treinamentoId, CancellationToken ct)
        => await ObterArquivo(treinamentoId, TipoArquivoTreinamentoCipa.ListaPresenca, ct);

    private async Task<IActionResult> ObterArquivo(Guid treinamentoId, TipoArquivoTreinamentoCipa tipo, CancellationToken ct)
    {
        var arquivo = await _mediator.Send(new ObterArquivoTreinamentoCipaQuery(treinamentoId, tipo), ct);
        return arquivo is null ? NotFound() : File(arquivo.Conteudo, arquivo.ContentType, arquivo.NomeArquivo);
    }
}

public record DefinirCargoMembroCipaRequestBody(CargoMembroCipa Cargo);
public record CriarTreinamentoCipaRequestBody(int CargaHoraria, string? ConteudoProgramatico, DateTime DataRealizacao, DateTime? DataValidade, string? InstituicaoInstrutor);
public class AnexarArquivoTreinamentoCipaRequestBody
{
    public IFormFile Arquivo { get; set; } = null!;
}

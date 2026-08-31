using AAHBRANT.SST.Application.Dds.Commands;
using AAHBRANT.SST.Application.Dds.Queries;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AAHBRANT.SST.Api.Controllers;

// Semana (contêiner) do DDS reformulado (31/08) — cada DDS diário (DdsController) continua sendo
// feito e assinado todo dia, mas só é "realmente finalizado" aqui, no fim da semana. "Responsável/
// Treinador" (criação) e "Responsável da Obra/SST" (encerramento) são sempre o usuário logado — sem
// corpo de requisição para esses campos, mesmo padrão de AssinarComSessaoLogada em AssinaturaController.
[ApiController]
[Route("api/[controller]")]
public class DdsSemanalController : ControllerBase
{
    private readonly IMediator _mediator;

    public DdsSemanalController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "dds:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarDdsSemanaisQuery(obraId), ct));

    [Authorize(Policy = "dds:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterDetalhe(Guid id, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterDdsSemanalDetalheQuery(id), ct);
        return detalhe is null ? NotFound() : Ok(detalhe);
    }

    [Authorize(Policy = "dds:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarDdsSemanalRequestBody body, CancellationToken ct)
    {
        var azureAdObjectId = ObterAzureAdObjectId();
        var command = new CriarDdsSemanalCommand(
            body.ObraId, body.Tipo, body.EmpresaTerceirizada, body.NumeroDocumento, body.LocalFrenteServico,
            body.DataInicioSemana, azureAdObjectId);
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterDetalhe), new { id }, new { id });
    }

    [Authorize(Policy = "dds:encerrar")]
    [HttpPost("{id:guid}/encerrar")]
    public async Task<IActionResult> Encerrar(Guid id, EncerrarDdsSemanalRequestBody body, CancellationToken ct)
    {
        var azureAdObjectId = ObterAzureAdObjectId();
        await _mediator.Send(new EncerrarDdsSemanalCommand(
            id, azureAdObjectId, body.ResponsavelEmpresaTerceirizadaNome, body.ResponsavelEmpresaTerceirizadaFuncao), ct);
        return NoContent();
    }

    [Authorize(Policy = "dds:exportar")]
    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> ExportarPdf(Guid id, CancellationToken ct)
    {
        var pdf = await _mediator.Send(new ExportarDdsSemanalPdfQuery(id), ct);
        return pdf is null ? NotFound() : File(pdf, "application/pdf", $"dds-semanal-{id}.pdf");
    }

    private string? ObterAzureAdObjectId()
        => User.FindFirst("oid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}

public record CriarDdsSemanalRequestBody(
    Guid ObraId,
    TipoDdsSemanal Tipo,
    string? EmpresaTerceirizada,
    string? NumeroDocumento,
    string? LocalFrenteServico,
    DateTime DataInicioSemana);

public record EncerrarDdsSemanalRequestBody(
    string? ResponsavelEmpresaTerceirizadaNome,
    string? ResponsavelEmpresaTerceirizadaFuncao);

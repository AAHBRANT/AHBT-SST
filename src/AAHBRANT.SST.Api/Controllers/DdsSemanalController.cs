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
    private readonly IWebHostEnvironment _environment;

    public DdsSemanalController(IMediator mediator, IWebHostEnvironment environment)
    {
        _mediator = mediator;
        _environment = environment;
    }

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
            body.ObraId, body.Tipo, body.EmpresaTerceirizada, body.LocalFrenteServico,
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

    // Assinatura com um clique (sessão logada) de um dos dois campos do documento semanal — um botão
    // para cada: "responsavel-dds" e "responsavel-obra-sst". O IP vem da conexão, nunca do cliente.
    [Authorize(Policy = "assinatura:assinar")]
    [HttpPost("{id:guid}/assinar/{papel}")]
    public async Task<IActionResult> Assinar(Guid id, string papel, CancellationToken ct)
    {
        PapelAssinatura? papelAssinatura = papel switch
        {
            "responsavel-dds" => PapelAssinatura.ResponsavelDds,
            "responsavel-obra-sst" => PapelAssinatura.ResponsavelObraSst,
            _ => null,
        };
        if (papelAssinatura is null)
            return BadRequest(new { erro = "Campo de assinatura inválido." });

        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        var ip = !string.IsNullOrWhiteSpace(forwardedFor)
            ? forwardedFor.Split(',')[0].Trim()
            : HttpContext.Connection.RemoteIpAddress?.ToString();

        var signatario = await _mediator.Send(new AssinarDdsSemanalCommand(id, papelAssinatura.Value, ObterAzureAdObjectId(), ip), ct);
        return Ok(signatario);
    }

    [Authorize(Policy = "dds:exportar")]
    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> ExportarPdf(Guid id, CancellationToken ct)
    {
        var pdf = await _mediator.Send(new ExportarDdsSemanalPdfQuery(id), ct);
        return pdf is null ? NotFound() : File(pdf, "application/pdf", $"dds-semanal-{id}.pdf");
    }

    // "Baixar semana": DDS semanal + o DDS diário (com lista de presença) de cada dia, num PDF só.
    [Authorize(Policy = "dds:exportar")]
    [HttpGet("{id:guid}/pdf-completo")]
    public async Task<IActionResult> ExportarPdfCompleto(Guid id, CancellationToken ct)
    {
        var pdf = await _mediator.Send(new ExportarDdsSemanaCompletaPdfQuery(id), ct);
        return pdf is null ? NotFound() : File(pdf, "application/pdf", $"dds-semana-completa-{id}.pdf");
    }

    // Em desenvolvimento o Entra ID está desligado e não há claim "oid": cai no usuário de dev, como
    // AlojamentosController.ObterOuCriarInspecaoAtual. Nunca vale fora de Development.
    private string? ObterAzureAdObjectId()
    {
        var id = User.FindFirst("oid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return string.IsNullOrWhiteSpace(id) && _environment.IsDevelopment() ? "dev-local-user" : id;
    }
}

public record CriarDdsSemanalRequestBody(
    Guid ObraId,
    TipoDdsSemanal Tipo,
    string? EmpresaTerceirizada,
    string? LocalFrenteServico,
    DateTime DataInicioSemana);

public record EncerrarDdsSemanalRequestBody(
    string? ResponsavelEmpresaTerceirizadaNome,
    string? ResponsavelEmpresaTerceirizadaFuncao);

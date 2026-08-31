using AAHBRANT.SST.Application.Assinatura.Commands;
using AAHBRANT.SST.Application.Assinatura.Queries;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AAHBRANT.SST.Api.Controllers;

// Motor de Assinatura Eletrônica (docs/Motor-Assinatura-Eletronica.md §3/§5) — controller genérico,
// não específico de nenhum módulo: EntidadeTipo/EntidadeId identificam o documento de origem (ex.:
// "Dds"). A tela de quiosque (AssinarDdsPage.tsx) é a primeira consumidora, mas o mesmo controller
// serve Treinamento/EPI/APR/PT/Inspeções quando entrarem no motor.
[ApiController]
[Route("api/documentos")]
public class AssinaturaController : ControllerBase
{
    private readonly IMediator _mediator;

    public AssinaturaController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "assinatura:ver")]
    [HttpGet]
    public async Task<IActionResult> Obter([FromQuery] string entidadeTipo, [FromQuery] Guid entidadeId, CancellationToken ct)
    {
        var documento = await _mediator.Send(new ObterDocumentoQuery(entidadeTipo, entidadeId), ct);
        return documento is null ? NotFound() : Ok(documento);
    }

    // Sub-rota (não o [HttpGet] raiz acima, que já é usado por Obter com entidadeTipo/entidadeId
    // obrigatórios) — painel administrativo (docs/Motor-Assinatura-Eletronica.md §5, etapa 12).
    [Authorize(Policy = "assinatura:ver")]
    [HttpGet("listar")]
    public async Task<IActionResult> Listar(
        [FromQuery] string? entidadeTipo,
        [FromQuery] StatusDocumentoAssinatura? status,
        [FromQuery] DateTime? dataInicio,
        [FromQuery] DateTime? dataFim,
        CancellationToken ct)
    {
        var documentos = await _mediator.Send(new ListarDocumentosAssinaturaQuery(entidadeTipo, status, dataInicio, dataFim), ct);
        return Ok(documentos);
    }

    [Authorize(Policy = "assinatura:assinar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarDocumentoAssinaturaCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return Ok(new { id });
    }

    // Assinatura em um clique do usuário logado (ex.: entregador de EPI assinando com a própria
    // sessão, sem crachá/PIN) — sem corpo de requisição, o TrabalhadorId é resolvido no handler a
    // partir do claim "oid" (mesmo padrão de VinculoAzureAdMiddleware/PermissaoAuthorizationHandler;
    // em dev com Entra ID desligado o claim não existe e o handler falha com mensagem amigável).
    [Authorize(Policy = "assinatura:assinar")]
    [HttpPost("{id:guid}/assinar/sessao")]
    public async Task<IActionResult> AssinarComSessaoLogada(Guid id, CancellationToken ct)
    {
        var azureAdObjectId = User.FindFirst("oid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var signatario = await _mediator.Send(new RegistrarAssinaturaSessaoLogadaCommand(id, azureAdObjectId), ct);
        return Ok(signatario);
    }

    public record AutenticarBiometriaLocalRequestBody(Guid DispositivoId, string SegredoDispositivo, Guid TrabalhadorId, double Score);

    [Authorize(Policy = "assinatura:assinar")]
    [HttpPost("{id:guid}/autenticacao/biometria-local")]
    public async Task<ActionResult<DocumentoSignatarioDto>> AutenticarBiometriaLocal(Guid id, AutenticarBiometriaLocalRequestBody body, CancellationToken ct)
    {
        var resultado = await _mediator.Send(
            new RegistrarAssinaturaBiometriaLocalCommand(id, body.DispositivoId, body.SegredoDispositivo, body.TrabalhadorId, body.Score, ObterIpCliente()), ct);
        return Ok(resultado);
    }

    // IP para o audit trail jurídico do Cofre de Assinaturas — nunca aceito do corpo da requisição
    // (evidência não pode ser controlada pelo cliente). Preferimos X-Forwarded-For porque a API roda
    // atrás de reverse proxy no Azure App Service; RemoteIpAddress é o fallback direto.
    private string? ObterIpCliente()
    {
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
            return forwardedFor.Split(',')[0].Trim();

        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    [Authorize(Policy = "assinatura:finalizar")]
    [HttpPost("{id:guid}/finalizar")]
    public async Task<IActionResult> Finalizar(Guid id, CancellationToken ct)
    {
        var documento = await _mediator.Send(new FinalizarDocumentoCommand(id), ct);
        return Ok(documento);
    }

    [Authorize(Policy = "assinatura:ver")]
    [HttpGet("{id:guid}/integridade")]
    public async Task<IActionResult> VerificarIntegridade(Guid id, CancellationToken ct)
    {
        var verificacao = await _mediator.Send(new VerificarIntegridadeQuery(id), ct);
        return Ok(verificacao);
    }

    [Authorize(Policy = "assinatura:ver")]
    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> ObterPdf(Guid id, CancellationToken ct)
    {
        var pdf = await _mediator.Send(new ObterPdfDocumentoQuery(id), ct);
        if (pdf is null) return NotFound();
        return File(pdf, "application/pdf", $"comprovante-assinatura-{id}.pdf");
    }
}


using AAHBRANT.SST.Application.Assinatura.Commands;
using AAHBRANT.SST.Application.Assinatura.Queries;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

    [Authorize(Policy = "assinatura:assinar")]
    [HttpPost("{id:guid}/assinar")]
    public async Task<IActionResult> Assinar(Guid id, AssinarRequestBody body, CancellationToken ct)
    {
        var signatario = await _mediator.Send(new RegistrarAssinaturaCommand(id, body.Uid, body.Pin), ct);
        return Ok(signatario);
    }

    // Assinatura biométrica WebAuthn/FIDO2 (etapa 13) — cerimônia em duas chamadas, espelhando o
    // cadastro em TrabalhadoresController: iniciar devolve o desafio (trabalhadorId nulo = leitor
    // compartilhado da obra, que só sabe quem assinou depois da resposta); confirmar autentica e
    // registra a assinatura via o mesmo IRegistradorAssinaturaService usado pelo fluxo crachá+PIN.
    [Authorize(Policy = "assinatura:assinar")]
    [HttpPost("assinar/webauthn/iniciar")]
    public async Task<IActionResult> IniciarAssinaturaWebAuthn([FromQuery] Guid? trabalhadorId, CancellationToken ct)
        => Ok(await _mediator.Send(new IniciarAssinaturaWebAuthnCommand(trabalhadorId), ct));

    [Authorize(Policy = "assinatura:assinar")]
    [HttpPost("{id:guid}/assinar/webauthn/confirmar")]
    public async Task<IActionResult> ConfirmarAssinaturaWebAuthn(Guid id, ConfirmarAssinaturaWebAuthnRequestBody body, CancellationToken ct)
    {
        var signatario = await _mediator.Send(new ConfirmarAutenticacaoWebAuthnCommand(id, body.OpcoesJson, body.RespostaJson), ct);
        return Ok(signatario);
    }

    public record AutenticarBiometriaLocalRequestBody(Guid DispositivoId, string SegredoDispositivo, Guid TrabalhadorId, double Score);

    [Authorize(Policy = "assinatura:assinar")]
    [HttpPost("{id:guid}/autenticacao/biometria-local")]
    public async Task<ActionResult<DocumentoSignatarioDto>> AutenticarBiometriaLocal(Guid id, AutenticarBiometriaLocalRequestBody body, CancellationToken ct)
    {
        var resultado = await _mediator.Send(
            new RegistrarAssinaturaBiometriaLocalCommand(id, body.DispositivoId, body.SegredoDispositivo, body.TrabalhadorId, body.Score), ct);
        return Ok(resultado);
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

public record AssinarRequestBody(string Uid, string Pin);
public record ConfirmarAssinaturaWebAuthnRequestBody(string OpcoesJson, string RespostaJson);

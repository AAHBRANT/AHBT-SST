using AAHBRANT.SST.Application.Assinatura.Commands;
using AAHBRANT.SST.Application.Trabalhadores.Commands;
using AAHBRANT.SST.Application.Trabalhadores.Queries;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TrabalhadoresController : ControllerBase
{
    private readonly IMediator _mediator;

    public TrabalhadoresController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "trabalhador:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarTrabalhadoresQuery(obraId), ct));

    [Authorize(Policy = "trabalhador:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var trabalhador = await _mediator.Send(new ObterTrabalhadorPorIdQuery(id), ct);
        return trabalhador is null ? NotFound() : Ok(trabalhador);
    }

    // Perfil de Vida do Trabalhador — agrega ASO/EPI/Treinamentos/Riscos/Ocorrências/Assinaturas numa
    // única chamada (ver ObterPerfilCompletoTrabalhadorQuery).
    [Authorize(Policy = "trabalhador:ver")]
    [HttpGet("{id:guid}/perfil-completo")]
    public async Task<IActionResult> ObterPerfilCompleto(Guid id, CancellationToken ct)
    {
        var perfil = await _mediator.Send(new ObterPerfilCompletoTrabalhadorQuery(id), ct);
        return perfil is null ? NotFound() : Ok(perfil);
    }

    [Authorize(Policy = "trabalhador:ver")]
    [HttpGet("{id:guid}/relatorio-pdf")]
    public async Task<IActionResult> ObterRelatorioFiscalizacao(Guid id, CancellationToken ct)
    {
        var pdf = await _mediator.Send(new GerarRelatorioFiscalizacaoTrabalhadorQuery(id), ct);
        if (pdf is null) return NotFound();
        return File(pdf, "application/pdf", $"relatorio-fiscalizacao-{id}.pdf");
    }

    [Authorize(Policy = "trabalhador:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarTrabalhadorCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }

    [Authorize(Policy = "trabalhador:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarTrabalhadorCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "trabalhador:excluir")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirTrabalhadorCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = "trabalhador:telegram")]
    [HttpPost("{id:guid}/telegram/vinculo")]
    public async Task<IActionResult> GerarVinculoTelegram(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new GerarVinculoTelegramCommand(id), ct));

    [Authorize(Policy = "trabalhador:assinatura")]
    [HttpPost("{id:guid}/assinatura/pin")]
    public async Task<IActionResult> DefinirPinAssinatura(Guid id, DefinirPinAssinaturaCommand command, CancellationToken ct)
    {
        if (id != command.TrabalhadorId) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "trabalhador:assinatura")]
    [HttpPost("{id:guid}/assinatura/termo-aceite")]
    public async Task<IActionResult> RegistrarTermoAceiteAssinatura(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new RegistrarTermoAceiteAssinaturaCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = "trabalhador:assinatura")]
    [HttpPost("{id:guid}/assinatura/consentimento-biometria")]
    public async Task<IActionResult> RegistrarConsentimentoBiometria(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new RegistrarConsentimentoBiometriaCommand(id), ct);
        return NoContent();
    }

    // Cadastro de credencial WebAuthn/FIDO2 (etapa 13) — cerimônia em duas chamadas: iniciar devolve o
    // desafio para o navegador repassar a navigator.credentials.create(); confirmar recebe a resposta
    // do autenticador e persiste a CredencialWebAuthn.
    [Authorize(Policy = "trabalhador:assinatura")]
    [HttpPost("{id:guid}/assinatura/webauthn/cadastro/iniciar")]
    public async Task<IActionResult> IniciarCadastroWebAuthn(Guid id, [FromQuery] TipoAutenticadorWebAuthn tipo, CancellationToken ct)
        => Ok(await _mediator.Send(new IniciarCadastroWebAuthnCommand(id, tipo), ct));

    [Authorize(Policy = "trabalhador:assinatura")]
    [HttpPost("{id:guid}/assinatura/webauthn/cadastro/confirmar")]
    public async Task<IActionResult> ConfirmarCadastroWebAuthn(Guid id, ConfirmarCadastroWebAuthnRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new ConfirmarCadastroWebAuthnCommand(id, body.Tipo, body.OpcoesJson, body.RespostaJson), ct);
        return NoContent();
    }

    public record CadastrarBiometriaLocalRequestBody(byte[] TemplateBruto);

    [Authorize(Policy = "trabalhador:assinatura")]
    [HttpPost("{id:guid}/assinatura/biometria-local/cadastro")]
    public async Task<IActionResult> CadastrarBiometriaLocal(Guid id, CadastrarBiometriaLocalRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new CadastrarTemplateBiometricoCommand(id, body.TemplateBruto), ct);
        return NoContent();
    }
}

public record ConfirmarCadastroWebAuthnRequestBody(TipoAutenticadorWebAuthn Tipo, string OpcoesJson, string RespostaJson);

using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Application.Riscos.Medidas;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

// Avaliação do risco e medidas de prevenção por fase (Ordem de Serviço, 20/10/2026). Quem confirma
// ou decide vem sempre do token. Perfil do responsável de SST ainda a definir: por ora vale risco:editar.
[ApiController]
[Route("api")]
public class MedidasPrevencaoController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUsuarioAtualResolver _usuarioAtual;

    public MedidasPrevencaoController(IMediator mediator, IUsuarioAtualResolver usuarioAtual)
    {
        _mediator = mediator;
        _usuarioAtual = usuarioAtual;
    }

    public record AdicionarMedidaRequest(FaseMedidaPrevencao Fase, string Texto);

    public record DecidirMedidaRequest(bool Aceitar, string? TextoEditado);

    [Authorize(Policy = "risco:editar")]
    [HttpPost("riscos/{riscoId:guid}/confirmar-avaliacao")]
    public async Task<IActionResult> ConfirmarAvaliacao(Guid riscoId, CancellationToken ct)
    {
        var usuarioId = await _usuarioAtual.ObterIdAsync(User, ct);
        await _mediator.Send(new ConfirmarAvaliacaoRiscoCommand(riscoId, usuarioId, User.FindFirst("name")?.Value), ct);
        return NoContent();
    }

    [Authorize(Policy = "risco:editar")]
    [HttpPost("riscos/{riscoId:guid}/medidas/separar-com-ia")]
    public async Task<IActionResult> SepararComIa(Guid riscoId, CancellationToken ct)
        => Ok(new { medidas = await _mediator.Send(new SepararMedidasRiscoCommand(riscoId), ct) });

    [Authorize(Policy = "risco:editar")]
    [HttpPost("riscos/{riscoId:guid}/medidas")]
    public async Task<IActionResult> Adicionar(Guid riscoId, AdicionarMedidaRequest corpo, CancellationToken ct)
    {
        var usuarioId = await _usuarioAtual.ObterIdAsync(User, ct);
        var id = await _mediator.Send(new AdicionarMedidaPrevencaoCommand(riscoId, corpo.Fase, corpo.Texto, usuarioId, User.FindFirst("name")?.Value), ct);
        return Ok(new { id });
    }

    [Authorize(Policy = "risco:editar")]
    [HttpPost("medidas-prevencao/{id:guid}/decidir")]
    public async Task<IActionResult> Decidir(Guid id, DecidirMedidaRequest corpo, CancellationToken ct)
    {
        var usuarioId = await _usuarioAtual.ObterIdAsync(User, ct);
        await _mediator.Send(new DecidirMedidaPrevencaoCommand(id, corpo.Aceitar, corpo.TextoEditado, usuarioId, User.FindFirst("name")?.Value), ct);
        return NoContent();
    }
}

using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Application.Atividades.DetalhesOs;
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

    public record SalvarDetalhesOsRequest(string? Condicoes, List<string> Ferramentas, List<Guid> EpcIds, List<Guid> DocumentoIds);

    public record AdicionarItemOsRequest(TipoItemAtividadeOs Tipo, string Texto, string? Requisito);

    public record DecidirItemOsRequest(bool Aceitar, string? TextoEditado, string? RequisitoEditado);

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

    // ---- Detalhes da atividade para a OS ----

    [Authorize(Policy = "risco:ver")]
    [HttpGet("atividades/{atividadeId:guid}/detalhes-os")]
    public async Task<IActionResult> ObterDetalhesOs(Guid atividadeId, CancellationToken ct)
    {
        var dto = await _mediator.Send(new ObterDetalhesOsAtividadeQuery(atividadeId), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [Authorize(Policy = "risco:editar")]
    [HttpPut("atividades/{atividadeId:guid}/detalhes-os")]
    public async Task<IActionResult> SalvarDetalhesOs(Guid atividadeId, SalvarDetalhesOsRequest corpo, CancellationToken ct)
    {
        await _mediator.Send(new SalvarDetalhesOsAtividadeCommand(atividadeId, corpo.Condicoes, corpo.Ferramentas, corpo.EpcIds, corpo.DocumentoIds), ct);
        return NoContent();
    }

    [Authorize(Policy = "risco:editar")]
    [HttpPost("atividades/{atividadeId:guid}/itens-os")]
    public async Task<IActionResult> AdicionarItemOs(Guid atividadeId, AdicionarItemOsRequest corpo, CancellationToken ct)
    {
        var usuarioId = await _usuarioAtual.ObterIdAsync(User, ct);
        var id = await _mediator.Send(new AdicionarItemAtividadeOsCommand(atividadeId, corpo.Tipo, corpo.Texto, corpo.Requisito, usuarioId, User.FindFirst("name")?.Value), ct);
        return Ok(new { id });
    }

    [Authorize(Policy = "risco:editar")]
    [HttpPost("atividades/{atividadeId:guid}/itens-os/sugerir-com-ia")]
    public async Task<IActionResult> SugerirItensOs(Guid atividadeId, CancellationToken ct)
        => Ok(new { itens = await _mediator.Send(new SugerirItensAtividadeOsCommand(atividadeId), ct) });

    [Authorize(Policy = "risco:editar")]
    [HttpPost("itens-os/{id:guid}/decidir")]
    public async Task<IActionResult> DecidirItemOs(Guid id, DecidirItemOsRequest corpo, CancellationToken ct)
    {
        var usuarioId = await _usuarioAtual.ObterIdAsync(User, ct);
        await _mediator.Send(new DecidirItemAtividadeOsCommand(id, corpo.Aceitar, corpo.TextoEditado, corpo.RequisitoEditado, usuarioId, User.FindFirst("name")?.Value), ct);
        return NoContent();
    }
}
